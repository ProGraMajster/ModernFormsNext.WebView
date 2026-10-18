using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernFormsNext.WebView.Sample;

internal static class RpcFailureRuntimeValidation
{
    private sealed class SlowRequest : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetInt32();
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        { Thread.Sleep(60); writer.WriteNumberValue(value); }
    }

    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p>RPC failure/lifetime validation</p>"));
        using var transport = new WebViewTransport(browser); using var rpc = new WebViewRpcClient(transport);
        await InstallAsync(transport);
        var metadata = new RpcSampleJson(new JsonSerializerOptions { Converters = { new SlowRequest() } });
        await ExpectErrorAsync(rpc.InvokeAsync("touch", 1, metadata.Int32, RpcSampleJson.Default.Int32,
            TimeSpan.FromMilliseconds(10)), WebViewRpcErrorCategory.Timeout);
        RuntimeValidation.Require(await Invoke(rpc, "count") == 0, "expired serialized request never reaches native page handler");
        await ExpectErrorAsync(Invoke(rpc, "wrong"), WebViewRpcErrorCategory.InvalidResponse);
        var rejected = Invoke(rpc, "reject");
        using (var caller = new CancellationTokenSource())
        {
            var canceled = rpc.InvokeAsync("never", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32, cancellationToken: caller.Token);
            var retained = Invoke(rpc, "echo"); caller.Cancel();
            await ExpectCancellationAsync(canceled); await ExpectErrorAsync(rejected, WebViewRpcErrorCategory.RemoteError);
            RuntimeValidation.Require(await retained == 42, "parallel cancellation/error does not cancel another RPC");
        }
        var stopped = Invoke(rpc, "never"); browser.Stop(); await ExpectCancellationAsync(stopped);
        await ReloadAsync(browser); await InstallAsync(transport);
        var disabled = Invoke(rpc, "never"); browser.IsJavaScriptEnabled = false;
        await ExpectCancellationAsync(disabled); browser.IsJavaScriptEnabled = true;
        await ReloadAsync(browser); await InstallAsync(transport);
        RuntimeValidation.Require(await Invoke(rpc, "echo") == 42, "same RPC client recovers after explicit reload/reinstallation");

        using (var retired = new WebViewRpcClient(transport))
        { var pending = Invoke(retired, "never"); retired.Dispose(); await ExpectCancellationAsync(pending); }
        RuntimeValidation.Require(await Invoke(rpc, "echo") == 42, "disposing another client retains borrowed transport");
        var disposed = Invoke(rpc, "never"); transport.Dispose(); await ExpectCancellationAsync(disposed);
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("42") == "42", "transport disposal retains native browser");
        report("PASS native RPC serialization deadline/no submission/typed conversion/async rejection/parallel cancel/Stop/JS disable/reload/client and transport disposal");
    }
    private static Task<int> Invoke(WebViewRpcClient rpc, string method) =>
        rpc.InvokeAsync(method, 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32);
    private static async Task InstallAsync(IWebViewTransport transport)
    {
        RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "true", "failure fixture endpoint installation");
        await transport.ExecuteScriptAsync("""
            window.rpcFailureTouches=0;
            modernFormsRpc.register('touch',()=>++window.rpcFailureTouches);
            modernFormsRpc.register('count',()=>window.rpcFailureTouches);
            modernFormsRpc.register('echo',()=>42);
            modernFormsRpc.register('never',()=>new Promise(()=>{}));
            modernFormsRpc.register('reject',()=>Promise.reject(new Error('private-page-detail')));
            modernFormsRpc.register('wrong',()=>({private:'private-page-detail'}));null
            """);
    }
    private static Task ReloadAsync(WebView browser) => RuntimeValidation.NavigateAsync(browser,
        () => RuntimeValidation.Require(browser.Reload(), "failure fixture reload"));
    private static async Task ExpectErrorAsync(Task<int> task, WebViewRpcErrorCategory category)
    {
        try { await task; throw new InvalidOperationException("Expected RPC error."); }
        catch (WebViewRpcException error)
        { RuntimeValidation.Require(error.Category == category && error.InnerException is null && !error.ToString().Contains("private-page-detail", StringComparison.Ordinal), "sanitized RPC failure"); }
    }
    private static async Task ExpectCancellationAsync(Task task)
    { try { await task; throw new InvalidOperationException("Expected cancellation."); } catch (OperationCanceledException) { } }
}
