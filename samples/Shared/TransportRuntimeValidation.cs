using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class TransportRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml(
            "<script>window.addEventListener('message',e=>{window.fromTransport=e.data;modernFormsWebView.postMessage('transport-ack');});</script>"));
        using var transport = new WebViewTransport(browser);
        RuntimeValidation.Require(transport.CurrentDocument.IsReady, "transport readiness uses completed document");
        var old = transport.CurrentDocument;
        using (var value = JsonDocument.Parse(await transport.ExecuteScriptAsync("({answer:42})")))
            RuntimeValidation.Require(value.RootElement.GetProperty("answer").GetInt32() == 42, "transport native JSON result");
        var ack = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int forwarded = 0;
        transport.MessageReceived += (_, e) =>
        {
            ++forwarded;
            RuntimeValidation.Require(!e.IsOriginAuthenticated && e.Document.RequestId == old.RequestId, "transport untrusted original context");
            if (e.Message == "transport-ack") ack.TrySetResult();
        };
        const string payload = "transport \" \\ 😀 </script>";
        await transport.PostMessageAsync(payload); await ack.Task.WaitAsync(TimeSpan.FromSeconds(15));
        RuntimeValidation.Require(JsonSerializer.Deserialize<string>(await transport.ExecuteScriptAsync("window.fromTransport")) == payload, "transport string escaping preserved");
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<h1>Transport replacement</h1>"));
        RuntimeValidation.Require(old.CancellationToken.IsCancellationRequested && transport.CurrentDocument.RequestId != old.RequestId, "transport canonical navigation invalidation");
        transport.Dispose();
        var fence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) => { if (e.Message == "transport-fence") fence.TrySetResult(); };
        browser.WebMessageReceived += received;
        try
        {
            await browser.ExecuteScriptAsync("modernFormsWebView.postMessage('transport-fence');null");
            await fence.Task.WaitAsync(TimeSpan.FromSeconds(15));
            RuntimeValidation.Require(forwarded == 1 && browser.IsInitialized, "disposed transport leaves baseline active");
        }
        finally { browser.WebMessageReceived -= received; }
        report("PASS native reusable transport JSON/strings/context invalidation/disposal + baseline compatibility");
    }
}
