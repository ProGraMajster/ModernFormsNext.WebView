using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

// A separate, explicitly named floor lane. Its ES5 actors can execute in the stock
// API23 image's Chromium44. The unchanged full suite remains an independent result;
// this lane never turns an unavailable modern RPC endpoint into a passing RPC test.
internal static class BaselineFloorRuntimeValidation
{
    internal static async Task RunAsync(SampleForm form, Action<string> report)
    {
        WebView browser = form.Browser;
        await RuntimeValidation.ReadyAsync(browser);
        RuntimeValidation.Require(browser.AllowedNavigationSchemes == WebViewNavigationSchemes.Default, "floor default schemes");
        bool api23 = browser.GetDiagnostics().Platform == "Android" && !OperatingSystem.IsAndroidVersionAtLeast(24);
        if (api23)
        {
            RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering), "API23 capability is absent");
            try { browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https; throw new InvalidOperationException("API23 accepted unsupported filter"); }
            catch (NotSupportedException) { }
            RuntimeValidation.Require(browser.AllowedNavigationSchemes == WebViewNavigationSchemes.Default, "unsupported setter preserves default");
            report("PASS floor API23 nondefault scheme UNSUPPORTED; Default retained");
        }
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<input id='floor-input'><script>window.floorAnswer=42;window.floorFrames=0;addEventListener('message',function(e){if(e.data==='floor-frame')++floorFrames;});</script>"));
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("floorAnswer") == "42", "floor ES5 script");
        using var transport = new WebViewTransport(browser);
        var document = transport.CurrentDocument;
        var messages = new List<WebViewMessageReceivedEventArgs>();
        EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) => { if (messages.Count < 16) messages.Add(e); };
        browser.WebMessageReceived += received;
        try
        {
            string same = "parent.modernFormsWebView.postMessage('floor-same');parent.postMessage('floor-frame','*');";
            string opaque = "__modernFormsStringBridge.postMessage(JSON.stringify([" + document.RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", 'floor-opaque']));parent.postMessage('floor-frame','*');";
            // On Windows only the main Core message channel is subscribed. This floor
            // fixture is Android-specific for the annotated opaque-frame bridge control.
            if (browser.GetDiagnostics().Platform != "Android")
                opaque = "parent.postMessage('floor-frame','*');";
            foreach (var actor in new[] { (Script: same, Sandboxed: false), (Script: opaque, Sandboxed: true) })
            {
                string html = JsonSerializer.Serialize("<script>" + actor.Script + "</script>");
                await browser.ExecuteScriptAsync("(function(){var f=document.createElement('iframe');" +
                    (actor.Sandboxed ? "f.setAttribute('sandbox','allow-scripts');" : "") + "f.srcdoc=" + html + ";document.body.appendChild(f);})();null");
            }
            await WaitAsync(browser, "floorFrames===2");
            await browser.ExecuteScriptAsync("modernFormsWebView.postMessage('floor-main');null");
            var fence = Stopwatch.StartNew();
            while ((!messages.Any(e => e.Message == "floor-main") || !messages.Any(e => e.Message == "floor-same") ||
                browser.GetDiagnostics().Platform == "Android" && !messages.Any(e => e.Message == "floor-opaque")) &&
                fence.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
            RuntimeValidation.Require(messages.Any(e => e.Message == "floor-main") && messages.Any(e => e.Message == "floor-same"), "actual floor main/same actors");
            if (browser.GetDiagnostics().Platform == "Android")
                RuntimeValidation.Require(messages.Any(e => e.Message == "floor-opaque"), "actual floor opaque annotated bridge actor");
            RuntimeValidation.Require(messages.All(e => !e.IsOriginAuthenticated && e.RequestId == document.RequestId), "floor actor messages remain untrusted");
        }
        finally { browser.WebMessageReceived -= received; }
        report("PASS floor actual ES5 main/same/opaque actors; no caller authentication");

        var content = CreateContent();
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        await WaitAsync(browser, "window.floorLocal===42");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!window.floorEvalDenied && !window.floorInline") == "true", "floor default CSP denies inline/eval");
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content, "two.html"));
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('two')") == "true", "floor second catalog resource actually rendered");
        RuntimeValidation.Require(browser.CanGoBack, "floor real local history");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.GoBack(), "floor back command"));
        RuntimeValidation.Require(browser.Source == content.GetUri("index.html"), "floor back target");
        await WaitAsync(browser, "window.floorLocal===42");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.GoForward(), "floor forward command"));
        RuntimeValidation.Require(browser.Source == content.GetUri("two.html"), "floor forward target");
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "floor reload command"));
        var prior = transport.CurrentDocument;
        browser.Stop();
        RuntimeValidation.Require(prior.CancellationToken.IsCancellationRequested && !browser.IsLoading && !transport.CurrentDocument.IsReady, "floor Stop lifetime");
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        await WaitAsync(browser, "window.floorLocal===42");
        browser.Select(); await browser.ExecuteScriptAsync("document.getElementById('floor-input').focus();null");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("document.activeElement.id") == "\"floor-input\"", "floor native input focus");
        browser.IsJavaScriptEnabled = false;
        bool rejected = false;
        try { _ = browser.ExecuteScriptAsync("42"); }
        catch (InvalidOperationException) { rejected = true; }
        RuntimeValidation.Require(rejected, "floor disabled script rejected");
        browser.IsJavaScriptEnabled = true;
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "floor JS recovery reload"));
        await WaitAsync(browser, "window.floorLocal===42");
        report("PASS floor local HTTPS/CSP/history/back/forward/reload/Stop/focus/JS setting recovery");
        for (int i = 0; i < 5; ++i)
        {
            using var temporary = new WebView { Bounds = new(2, 2, 80, 60), IsJavaScriptEnabled = true };
            form.Controls.Add(temporary);
            try { await RuntimeValidation.ReadyAsync(temporary); await RuntimeValidation.NavigateAsync(temporary, () => temporary.NavigateToHtml("<p>floor lease</p>")); }
            finally { form.Controls.Remove(temporary); }
        }
        string features = await browser.ExecuteScriptAsync("typeof AbortController==='function'&&typeof Map==='function'&&typeof Promise==='function'&&typeof TextEncoder==='function'");
        report("PASS floor five create/dispose leases; RPC modern prerequisites=" + features + " (separate from full-suite qualification)");
        RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "floor no privileged capability");
    }

    internal static WebViewLocalContent CreateContent() => new(new Dictionary<string, byte[]>
    {
        ["index.html"] = Encoding.UTF8.GetBytes("<input id='floor-input'><p>floor local</p><script>window.floorInline=true;</script><script src='floor.js'></script>"),
        ["two.html"] = Encoding.UTF8.GetBytes("<p id='two'>second history entry</p>"),
        ["floor.js"] = Encoding.UTF8.GetBytes("window.floorLocal=42;try{eval('1+1')}catch(e){window.floorEvalDenied=true;}")
    });

    internal static async Task NativeBackAsync(SampleForm form, Action<string> report)
    {
        WebView browser = form.Browser;
        var content = CreateContent();
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content, "two.html"));
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('two')") == "true", "native Back second catalog resource rendered");
        var back = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool requested = false;
        EventHandler<KeyEventArgs> handler = (_, e) =>
        {
            if (e.KeyCode != Keys.BrowserBack) return;
            e.Handled = true;
            // The canonical key can bubble from the focused browser to its Form.
            // One physical Back must select exactly one application navigation.
            if (requested) return;
            requested = true;
            if (browser.GoBack()) back.TrySetResult();
            else back.TrySetException(new InvalidOperationException("Native Back reached the app but history was unavailable"));
        };
        browser.Select(); browser.KeyDown += handler; form.KeyDown += handler;
        try
        {
            report("FLOOR WAIT NATIVE BACK (driver must verify foreground sample before selected-device keyevent)");
            await back.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await WaitAsync(browser, "!!document.getElementById('floor-input')");
            RuntimeValidation.Require(browser.Source == content.GetUri("index.html"), "native Back uses app-owned history decision");
            report("PASS floor actual system Back -> canonical key route -> explicit app GoBack");
        }
        finally { browser.KeyDown -= handler; form.KeyDown -= handler; }
    }

    internal static async Task WaitAsync(WebView browser, string predicate)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15))
        { if (await browser.ExecuteScriptAsync("!!(" + predicate + ")") == "true") return; await Task.Delay(20); }
        throw new TimeoutException("Floor actor/control did not execute: " + predicate);
    }
}
