using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class MessageSecurityRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        long id = 0;
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => id = e.RequestId;
        browser.NavigationStarting += starting;
        try
        {
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<h1>Messaging security fixture</h1>"));
            long originalId = id;
            var messages = new List<WebViewMessageReceivedEventArgs>();
            var fence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) =>
            { messages.Add(e); if (e.Message == "security-fence") fence.TrySetResult(); };
            browser.WebMessageReceived += received;
            try
            {
                await browser.ExecuteScriptAsync("window.frameDone=0;addEventListener('message',e=>{if(e.data==='security-frame-done')++frameDone;});null");
                await AddFrameAsync(browser, "parent.modernFormsWebView.postMessage('security-parent-helper');parent.postMessage('security-frame-done','*');", false);
                await AddFrameAsync(browser, NativeSend(originalId, "security-opaque") + "parent.postMessage('security-frame-done','*');", true);
                await WaitAsync(browser, "window.frameDone===2");
                await browser.ExecuteScriptAsync("modernFormsWebView.postMessage('security-fence');null");
                await fence.Task.WaitAsync(TimeSpan.FromSeconds(15));
                RuntimeValidation.Require(messages.Any(e => e.Message == "security-parent-helper"), "same-origin frame can call parent helper");
                if (browser.GetDiagnostics().Platform == "Android")
                    RuntimeValidation.Require(messages.Any(e => e.Message == "security-opaque" && e.Source is null), "Android opaque frame reaches untrusted bridge");
                else RuntimeValidation.Require(!messages.Any(e => e.Message == "security-opaque"), "Windows frame channel is not subscribed");
                RuntimeValidation.Require(messages.All(e => !e.IsOriginAuthenticated && e.RequestId == originalId && e.PeerGeneration > 0), "frame data never authenticated");
                report("PASS native same-origin parent-helper + opaque iframe trust matrix");

                await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<h1>Replacement document</h1>"));
                fence = new(TaskCreationOptions.RunContinuationsAsynchronously); messages.Clear();
                await browser.ExecuteScriptAsync(NativeSend(originalId, "security-stale") + "modernFormsWebView.postMessage('security-fence');null");
                await fence.Task.WaitAsync(TimeSpan.FromSeconds(15));
                RuntimeValidation.Require(!messages.Any(e => e.Message == "security-stale"), "old document packet rejected after replacement");
                RuntimeValidation.Require(messages.All(e => !e.IsOriginAuthenticated && e.RequestId == id), "current message remains untrusted");
                RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "privileged capability unavailable");
                report("PASS native stale document packet rejected + fail-closed authentication capability");
            }
            finally { browser.WebMessageReceived -= received; }
        }
        finally { browser.NavigationStarting -= starting; }
    }

    internal static async Task RunRedirectAsync(WebView browser, Uri destination, Action<string> report)
    {
        // A real page-initiated replacement is available without weakening Android cleartext/TLS policy.
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ids = new HashSet<long>();
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => ids.Add(e.RequestId);
        EventHandler<WebViewNavigationCompletedEventArgs> done = (_, e) => { if (e.Uri?.Host == destination.Host) completed.TrySetResult(); };
        browser.NavigationStarting += starting; browser.NavigationCompleted += done;
        try
        {
            browser.NavigateToHtml("<meta http-equiv='refresh' content='0;url=" + destination.AbsoluteUri + "'>");
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            RuntimeValidation.Require(ids.Count >= 2, "page redirect creates a distinct document identity");
            RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "redirect cannot authenticate bridge");
            report("PASS native meta redirect/document identity + no authentication upgrade");
        }
        finally { browser.NavigationStarting -= starting; browser.NavigationCompleted -= done; }
    }

    internal static async Task RunNetworkAsync(WebView browser, Uri redirect, Uri foreignFrame, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.Navigate(redirect));
        RuntimeValidation.Require(browser.Source?.Host == "localhost", "real HTTP302 cross-origin destination");
        long id = 0; EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => id = e.RequestId;
        browser.NavigationStarting += starting;
        try { await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<h1>Foreign frame fixture</h1>")); }
        finally { browser.NavigationStarting -= starting; }
        int foreign = 0;
        EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) =>
        { if (e.Message == "security-foreign") ++foreign; RuntimeValidation.Require(!e.IsOriginAuthenticated, "foreign source never authenticated"); };
        browser.WebMessageReceived += received;
        try
        {
            await browser.ExecuteScriptAsync("window.frameDone=0;addEventListener('message',e=>{if(e.data==='security-frame-done')++frameDone;});" +
                "(()=>{const f=document.createElement('iframe');f.src=" + JsonSerializer.Serialize(foreignFrame.AbsoluteUri + "?id=" + id) + ";document.body.append(f);})();null");
            await WaitAsync(browser, "window.frameDone===1");
            RuntimeValidation.Require(foreign == 0, "Windows foreign frame has no subscribed native frame channel");
            report("PASS native HTTP302 cross-origin + foreign HTTP iframe (no frame subscription)");
        }
        finally { browser.WebMessageReceived -= received; }
    }

    private static Task AddFrameAsync(WebView browser, string script, bool opaque) => browser.ExecuteScriptAsync(
        "(()=>{const f=document.createElement('iframe');" + (opaque ? "f.sandbox='allow-scripts';" : "") +
        "f.srcdoc=" + JsonSerializer.Serialize("<script>" + script + "</script>") + ";document.body.append(f);})();null");
    private static string NativeSend(long id, string payload) =>
        "(()=>{const p=JSON.stringify([" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + JsonSerializer.Serialize(payload) +
        "]);if(typeof __modernFormsStringBridge!=='undefined')__modernFormsStringBridge.postMessage(p);else chrome.webview.postMessage(p);})();";
    private static async Task WaitAsync(WebView browser, string predicate)
    {
        for (int i = 0; i < 100; ++i)
        { if (await browser.ExecuteScriptAsync(predicate) == "true") return; await Task.Delay(50); }
        throw new InvalidOperationException("Native security fixture did not execute its frame script.");
    }
}
