using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class LocalContentRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        var content = LocalContentSample.Create();
        RuntimeValidation.Require(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.LocalContent), "local interception capability");
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        await AssertDocumentAsync(browser);
        report("PASS local HTML/CSS/JS/image + CSP inline/frame denial");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "local reload"));
        await AssertDocumentAsync(browser);
        var replacement = LocalContentSample.Create();
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(replacement));
        RuntimeValidation.Require(browser.Source?.Host == replacement.Origin.Host, "replacement origin");
        await AssertDocumentAsync(browser);
        report("PASS local reload + collection replacement");
        var canceled = LocalContentSample.Create();
        var cancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationStartingEventArgs> cancel = (_, e) =>
        { if (e.Uri?.Host == canceled.Origin.Host) { e.Cancel = true; cancellation.TrySetResult(); } };
        browser.NavigationStarting += cancel;
        try
        {
            browser.NavigateToLocalContent(canceled);
            await cancellation.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { browser.NavigationStarting -= cancel; }
        RuntimeValidation.Require(browser.Source?.Host == replacement.Origin.Host, "cancel restores local logical request");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "reload after canceled collection"));
        await AssertDocumentAsync(browser);
        report("PASS canceled collection replacement + reload restores resource access");
        // Fetch is intentionally forbidden, including same-origin requests. A public resource
        // collection is a static document surface, not an RPC or network endpoint.
        await browser.ExecuteScriptAsync("window.fetchBlocked=false;fetch('app.js').catch(()=>window.fetchBlocked=true);null");
        for (int i = 0; i < 40 && await browser.ExecuteScriptAsync("window.fetchBlocked") != "true"; ++i)
            await Task.Delay(50);
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("window.fetchBlocked") == "true", "CSP connect denial");
        report("PASS local CSP fetch denial");
        var denied = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationCompletedEventArgs> completed = (_, _) => denied.TrySetResult(true);
        EventHandler<WebViewNavigationFailedEventArgs> failed = (_, _) => denied.TrySetResult(false);
        browser.NavigationCompleted += completed; browser.NavigationFailed += failed;
        try
        {
            // Missing resources use an empty native response, never a network fallback.
            browser.Navigate(new Uri(replacement.Origin, "missing.html"));
            // WebView2 reports a failed navigation for an empty 410; Android may finish
            // an empty error document. Neither is a successful application document.
            if (await denied.Task.WaitAsync(TimeSpan.FromSeconds(15)))
                RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('title')") == "false", "inactive origin exposes no application document");
        }
        finally { browser.NavigationCompleted -= completed; browser.NavigationFailed -= failed; }
        report("PASS inactive reserved origin denied locally");
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        browser.Stop(); RuntimeValidation.Require(!browser.IsLoading, "local Stop");
    }

    private static async Task AssertDocumentAsync(WebView browser)
    {
        using var result = JsonDocument.Parse(await browser.ExecuteScriptAsync("({answer:window.localAnswer," +
            "color:getComputedStyle(document.getElementById('title')).color," +
            "image:document.getElementById('image').naturalWidth,missing:!!window.missingDenied,inline:!!window.inlineExecuted,frame:!!window.frameExecuted})"));
        var value = result.RootElement;
        RuntimeValidation.Require(value.GetProperty("answer").GetInt32() == 42, "external script loaded");
        RuntimeValidation.Require(value.GetProperty("color").GetString() == "rgb(20, 70, 120)", "external CSS loaded");
        RuntimeValidation.Require(value.GetProperty("image").GetInt32() == 1, "binary local image loaded");
        RuntimeValidation.Require(value.GetProperty("missing").GetBoolean(), "missing local image denied");
        RuntimeValidation.Require(!value.GetProperty("inline").GetBoolean() && !value.GetProperty("frame").GetBoolean(), "CSP blocks inline code and frames");
    }
}
