using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModernFormsNext.WebView.Sample;

internal sealed record RpcSampleRequest(int Value, int Delay);
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(RpcSampleRequest))]
internal partial class RpcSampleJson : JsonSerializerContext;

internal static class RpcRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p>Explicit unprivileged RPC endpoint</p>"));
        using var transport = new WebViewTransport(browser);
        RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "true", "RPC endpoint feature/install check");
        RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "true", "RPC installation idempotent");
        await transport.ExecuteScriptAsync("""
            modernFormsRpc.register('echo', value => value);
            modernFormsRpc.register('delay', value => new Promise(resolve => setTimeout(() => resolve(value.Value), value.Delay)));
            modernFormsRpc.register('fail', () => { throw new Error('private-page-error'); });
            modernFormsRpc.register('never', () => new Promise(() => {}));
            modernFormsRpc.register('cancel', (_, signal) => new Promise(resolve => {
              window.rpcStarted = true;
              signal.addEventListener('abort', () => { window.rpcAborted = true; resolve(-1); }, {once:true});
            }));
            null
            """);
        using var rpc = new WebViewRpcClient(transport);
        const string text = "RPC \" \\ Zażółć 😀 </script>";
        RuntimeValidation.Require(await rpc.InvokeAsync("echo", text, RpcSampleJson.Default.String, RpcSampleJson.Default.String) == text, "RPC typed string escaping");
        var slow = rpc.InvokeAsync("delay", new RpcSampleRequest(1, 1000), RpcSampleJson.Default.RpcSampleRequest, RpcSampleJson.Default.Int32);
        var fast = rpc.InvokeAsync("delay", new RpcSampleRequest(2, 0), RpcSampleJson.Default.RpcSampleRequest, RpcSampleJson.Default.Int32);
        RuntimeValidation.Require(await fast == 2 && !slow.IsCompleted && await slow == 1, "RPC out-of-order correlation");
        await ExpectErrorAsync(() => rpc.InvokeAsync("missing", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32), WebViewRpcErrorCategory.RemoteError, "method_not_found");
        await ExpectErrorAsync(() => rpc.InvokeAsync("fail", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32), WebViewRpcErrorCategory.RemoteError, "handler_error");
        await ExpectErrorAsync(() => rpc.InvokeAsync("never", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32, TimeSpan.FromMilliseconds(100)), WebViewRpcErrorCategory.Timeout);
        using (var caller = new CancellationTokenSource())
        {
            var canceled = rpc.InvokeAsync("cancel", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32, cancellationToken: caller.Token);
            await WaitForScriptAsync(transport, "window.rpcStarted===true"); caller.Cancel();
            await ExpectCancellationAsync(canceled);
            await WaitForScriptAsync(transport, "window.rpcAborted===true");
        }
        // Exercise endpoint-side validation through actual MessageEvents, including escaped
        // duplicate names. Only the final valid request may execute its page-only handler.
        await transport.ExecuteScriptAsync("modernFormsRpc.register('count',()=>{window.rpcCount=(window.rpcCount||0)+1;return 1;});null");
        var current = transport.CurrentDocument;
        string documentId = FormattableString.Invariant($"{current.PeerGeneration}:{current.RequestId}");
        var request = "{\"version\":1,\"type\":\"request\",\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"document\":\"" +
            documentId + "\",\"method\":\"count\",\"payload\":null,\"timeoutMs\":10000}";
        await transport.PostMessageAsync(request.Insert(1, "\"\\u0076ersion\":1,"));
        await transport.PostMessageAsync(request.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal));
        await transport.PostMessageAsync("{bad"); await transport.PostMessageAsync(new string('x', 65537));
        await transport.PostMessageAsync(request); await WaitForScriptAsync(transport, "window.rpcCount===1");
        await transport.PostMessageAsync(request);
        RuntimeValidation.Require(await transport.ExecuteScriptAsync("window.rpcCount") == "1", "RPC invalid/duplicate envelopes do not invoke page handler twice");
        await transport.ExecuteScriptAsync("window.dispatchEvent(new Event('pagehide'));null");
        RuntimeValidation.Require(await transport.ExecuteScriptAsync("typeof window.modernFormsRpc==='undefined'") == "true", "RPC pagehide retires endpoint including cached global");
        await transport.ExecuteScriptAsync("window.savedAbortController=window.AbortController;window.AbortController=undefined;null");
        try { RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "false", "RPC unsupported feature detection"); }
        finally { await transport.ExecuteScriptAsync("window.AbortController=window.savedAbortController;delete window.savedAbortController;null"); }
        RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "true", "RPC explicit reinstall after pagehide");
        await transport.ExecuteScriptAsync("modernFormsRpc.register('never',()=>new Promise(()=>{}));null");
        var old = transport.CurrentDocument;
        var obsolete = rpc.InvokeAsync("never", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32);
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p>RPC replacement</p>"));
        await ExpectCancellationAsync(obsolete);
        RuntimeValidation.Require(old.CancellationToken.IsCancellationRequested &&
            await transport.ExecuteScriptAsync("typeof window.modernFormsRpc==='undefined'") == "true", "RPC lifetime revocation + no automatic endpoint injection");
        rpc.Dispose();
        RuntimeValidation.Require(await transport.ExecuteScriptAsync("42") == "42", "RPC disposal borrows transport and browser");
        report("PASS native RPC typed JSON/out-of-order/safe errors/timeout/cancel signal/validation/duplicates/pagehide/features/document replacement + borrowed lifetime");
    }

    private static async Task ExpectErrorAsync(Func<Task<int>> operation, WebViewRpcErrorCategory category, string? code = null)
    {
        try { await operation(); throw new InvalidOperationException("Expected RPC failure."); }
        catch (WebViewRpcException error)
        {
            RuntimeValidation.Require(error.Category == category && (code is null || error.RemoteCode == code) &&
                error.InnerException is null && !error.ToString().Contains("private-page-error", StringComparison.Ordinal), "RPC sanitized failure category");
        }
    }
    private static async Task ExpectCancellationAsync(Task task)
    {
        try { await task; throw new InvalidOperationException("Expected RPC cancellation."); }
        catch (OperationCanceledException) { }
    }
    private static async Task WaitForScriptAsync(IWebViewTransport transport, string predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await transport.ExecuteScriptAsync(predicate, deadline.Token) != "true") await Task.Delay(20, deadline.Token);
    }
}
