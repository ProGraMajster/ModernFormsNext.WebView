using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class NavigationSchemeRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        RuntimeValidation.Require(browser.AllowedNavigationSchemes == WebViewNavigationSchemes.Default, "scheme default compatibility");
        if (!RequireCapability(browser, report)) return;
        using var transport = new WebViewTransport(browser);
        var original = browser.AllowedNavigationSchemes;
        try
        {
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https | WebViewNavigationSchemes.AboutBlank;
            var before = transport.CurrentDocument;
            try { browser.Navigate(new Uri("http://navigation-denied.invalid/")); throw new InvalidOperationException("HTTP command accepted"); }
            catch (ArgumentException) { }
            RuntimeValidation.Require(before == transport.CurrentDocument && !before.CancellationToken.IsCancellationRequested,
                "app URI rejection is before native submission/canonical invalidation");

            // Exclude HTTPS for real page-initiated attacks. Android's unchanged
            // cleartext=false manifest would independently deny an HTTP attempt.
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Http | WebViewNavigationSchemes.AboutBlank;
            var forbidden = new Uri("https://navigation-denied.invalid/");
            foreach (string actor in new[] { "main", "same", "opaque" })
            {
                await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(Actors(forbidden)));
                await WaitScriptAsync(browser, "window.probeReady.same===true&&window.probeReady.opaque===true");
                var document = transport.CurrentDocument;
                RuntimeValidation.Require(document.IsReady, "positive current raw fixture");
                string attack = actor == "main" ? "attackMain();null"
                    : "document.getElementById('" + actor + "').contentWindow.postMessage('attack','*');null";
                await RejectAsync(browser, () => browser.ExecuteScriptAsync(attack), forbidden);
                await WaitScriptAsync(browser, "window.probeExecuted." + actor + "===1");
                RuntimeValidation.Require(document.CancellationToken.IsCancellationRequested && !transport.CurrentDocument.IsReady,
                    "reported denial revokes canonical document operations");
                RuntimeValidation.Require(browser.Source is null && !browser.IsLoading, "retained raw logical document after GET denial");
                browser.Stop();
                await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "raw recovery reload"));
                report("PASS scheme filter real " + actor + " actor executes; top-level HTTPS rejected before public Starting + Stop/reload");
            }
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.None;
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p id='raw-policy'>explicit raw operation</p>"));
            await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "None still permits explicit raw reload"));
            RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('raw-policy')") == "true", "raw private URL normalization");
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https | WebViewNavigationSchemes.AboutBlank;
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(LocalContentSample.Create()));
            RuntimeValidation.Require(await browser.ExecuteScriptAsync("window.localAnswer") == "42", "allowed HTTPS catalog positive control");
            RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "no authenticated capability");
            report("PASS scheme filter None/raw separation + HTTPS local content + unchanged unprivileged boundary");
        }
        finally { browser.AllowedNavigationSchemes = original; }
    }

    internal static async Task RunPostAsync(WebView browser, Uri destination, Action<string> report)
    {
        if (!RequireCapability(browser, report)) return;
        var original = browser.AllowedNavigationSchemes;
        try
        {
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.AboutBlank |
                (destination.Scheme == "https" ? WebViewNavigationSchemes.Http : WebViewNavigationSchemes.Https);
            string html = "<form method='post' action='" + WebUtility.HtmlEncode(destination.AbsoluteUri) + "'></form>";
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(html));
            await RejectAsync(browser, () => browser.ExecuteScriptAsync("document.forms[0].submit();null"), destination);
            // Android's OnPageStarted is a stop fallback AFTER POST network effects.
            report("PASS scheme filter actual POST reported/denied (Android fallback is not pre-network)");
        }
        finally { browser.AllowedNavigationSchemes = original; }
    }

    internal static async Task RunRedirectAsync(WebView browser, Uri initial, Action<string> report)
    {
        var original = browser.AllowedNavigationSchemes;
        try
        {
            browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Http | WebViewNavigationSchemes.AboutBlank;
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p>before redirect</p>"));
            bool initialStarted = false;
            EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) =>
            { if (e.Uri == initial && !e.IsRedirect) initialStarted = true; };
            browser.NavigationStarting += starting;
            try
            {
                await RejectAsync(browser, () => { browser.Navigate(initial); return Task.FromResult("null"); },
                    new Uri("https://navigation-denied.invalid/"));
                RuntimeValidation.Require(initialStarted, "owned HTTP302 initial request was accepted");
            }
            finally { browser.NavigationStarting -= starting; }
            report("PASS scheme filter actual HTTP302 to forbidden HTTPS (no DNS/TLS bypass)");
        }
        finally { browser.AllowedNavigationSchemes = original; }
    }

    private static async Task RejectAsync(WebView browser, Func<Task<string>> action, Uri target)
    {
        var failed = new TaskCompletionSource<WebViewNavigationFailedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        int exposed = 0;
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => { if (e.Uri == target) ++exposed; };
        EventHandler<WebViewNavigationFailedEventArgs> failure = (_, e) => failed.TrySetResult(e);
        browser.NavigationStarting += starting; browser.NavigationFailed += failure;
        try
        {
            Task<string> submitted = action();
            var result = await failed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            try { await submitted; } catch (OperationCanceledException) { }
            RuntimeValidation.Require(result.Category == WebViewErrorCategory.InvalidAddress && result.Uri == target,
                "typed scheme rejection from real native callback");
            RuntimeValidation.Require(exposed == 0, "forbidden URI is not exposed as accepted public Starting");
        }
        finally { browser.NavigationStarting -= starting; browser.NavigationFailed -= failure; }
    }

    private static async Task WaitScriptAsync(WebView browser, string predicate)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (await browser.ExecuteScriptAsync("!!(" + predicate + ")") == "true") return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Native page actor positive control did not complete: " + predicate);
    }

    private static bool RequireCapability(WebView browser, Action<string> report)
    {
        if (browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering)) return true;
        RuntimeValidation.Require(browser.GetDiagnostics().Platform == "Android" && !OperatingSystem.IsAndroidVersionAtLeast(24),
            "supported native engine must advertise its installed main-frame hooks");
        try { browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https; throw new InvalidOperationException("Unsupported option accepted"); }
        catch (NotSupportedException) { }
        RuntimeValidation.Require(browser.AllowedNavigationSchemes == WebViewNavigationSchemes.Default, "unsupported setter leaves default intact");
        report("PASS explicit NavigationSchemeFiltering UNSUPPORTED on API23; default baseline continues");
        return false;
    }

    private static string Actors(Uri target)
    {
        string destination = JsonSerializer.Serialize(target.AbsoluteUri);
        string Frame(string actor) => $$$"""
            <script>
            parent.postMessage('ready:{{{actor}}}','*');
            addEventListener('message',e=>{if(e.data==='attack'){
              parent.postMessage('executed:{{{actor}}}','*');top.location.href={{{destination}}};
            }});
            </script>
            """;
        return $$$"""
            <script>
            window.probeReady={};window.probeExecuted={main:0,same:0,opaque:0};
            addEventListener('message',e=>{if(typeof e.data==='string'){const p=e.data.split(':');
              if(p[0]==='ready')probeReady[p[1]]=true;
              if(p[0]==='executed')probeExecuted[p[1]]++;}});
            function attackMain(){probeExecuted.main++;location.href={{{destination}}};}
            </script>
            <iframe id='same' srcdoc="{{{WebUtility.HtmlEncode(Frame("same"))}}}"></iframe>
            <iframe id='opaque' sandbox='allow-scripts allow-top-navigation' srcdoc="{{{WebUtility.HtmlEncode(Frame("opaque"))}}}"></iframe>
            """;
    }
}
