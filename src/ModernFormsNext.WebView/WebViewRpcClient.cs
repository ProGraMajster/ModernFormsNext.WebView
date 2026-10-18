using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView;

/// <summary>An experimental bounded client for explicitly registered JavaScript handlers.</summary>
/// <remarks>
/// Borrow the transport, invoke and dispose on its UI thread. No C# handler registry exists.
/// Replies are untrusted data even when correlated; never use them as authorization.
/// JsonTypeInfo is mandatory for explicit trimming/AOT-compatible serialization.
/// Response converters can run off-thread and must not access UI controls.
/// Cancellation revokes local waits and sends only an advisory cancel to the original document.
/// Already submitted effects may continue; no retries, replay or script installation occur.
/// </remarks>
/// <example><code>
/// using var transport = new WebViewTransport(browser);
/// await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript);
/// // The page explicitly registers a JavaScript handler named "echo".
/// using var rpc = new WebViewRpcClient(transport);
/// string? answer = await rpc.InvokeAsync("echo", "hello", MyJsonContext.Default.String, MyJsonContext.Default.String);
/// </code></example>
public sealed class WebViewRpcClient : IDisposable
{
    private sealed record Pending(WebViewDocumentContext Document, TaskCompletionSource<WebViewRpcProtocol.Reply> Completion);
    private readonly IWebViewTransport transport;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationToken transportToken;
    private readonly Dictionary<string, Pending> pending = [];
    private readonly object sync = new();
    private bool disposed;

    /// <summary>Creates a client borrowing a transport on its owning UI thread.</summary>
    public WebViewRpcClient(IWebViewTransport transport)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _ = transport.CurrentDocument;
        transportToken = transport.LifetimeToken; lifetimeToken = lifetime.Token;
        transport.MessageReceived += Receive;
    }

    /// <summary>Invokes one JavaScript method and converts its untrusted JSON result using explicit metadata.</summary>
    /// <param name="method">Allowlisted page method name: 1–128 ASCII letters, digits, underscore, dot or hyphen.</param>
    /// <param name="request">JSON-compatible request data; do not send secrets to untrusted content.</param>
    /// <param name="requestTypeInfo">Explicit request serialization metadata, normally source-generated.</param>
    /// <param name="responseTypeInfo">Explicit response metadata; conversion is not authorization.</param>
    /// <param name="timeout">Total local deadline including JSON conversion and submission, 1–120000 milliseconds; null uses 10000.</param>
    /// <param name="cancellationToken">Cancels the local wait; cancellation cannot undo submitted JavaScript effects.</param>
    /// <returns>The typed response, or cancellation/sanitized WebViewRpcException.</returns>
    /// <remarks>
    /// UI-thread entry. Requires a successfully completed, JS-enabled document and explicit endpoint installation.
    /// Synchronous converters cannot be interrupted: their elapsed time consumes the deadline and is checked
    /// before submission/return. Cancellation takes precedence over a coincident converter or engine failure.
    /// </remarks>
    public Task<TResponse?> InvokeAsync<TRequest, TResponse>(string method, TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(requestTypeInfo); ArgumentNullException.ThrowIfNull(responseTypeInfo);
        if (!WebViewRpcProtocol.ValidMethod(method)) throw new ArgumentException("Invalid RPC method name.", nameof(method));
        double milliseconds = (timeout ?? TimeSpan.FromMilliseconds(WebViewRpcProtocol.DefaultTimeoutMilliseconds)).TotalMilliseconds;
        if (milliseconds < 1 || milliseconds > WebViewRpcProtocol.MaximumTimeoutMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var document = transport.CurrentDocument;
        if (!document.IsReady || document.RequestId <= 0 || document.PeerGeneration <= 0)
            throw new InvalidOperationException("RPC requires a completed JavaScript-enabled document.");
        return InvokeCoreAsync(method, request, requestTypeInfo, responseTypeInfo, document, (int)Math.Ceiling(milliseconds), cancellationToken);
    }

    private async Task<TResponse?> InvokeCoreAsync<TRequest, TResponse>(string method, TRequest request,
        JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo,
        WebViewDocumentContext document, int timeout, CancellationToken caller)
    {
        long started = Stopwatch.GetTimestamp();
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, document.CancellationToken, transportToken, lifetimeToken, deadline.Token);
        string id = Guid.NewGuid().ToString("N"), documentId = WebViewRpcProtocol.DocumentId(document), message;
        bool registered = false, submitted = false, canceled = false;
        void CheckActive()
        {
            linked.Token.ThrowIfCancellationRequested();
            // Timers cannot preempt a synchronous application converter and may dispatch
            // late under thread-pool pressure. Measure elapsed time as well; never send
            // an expired request or return a result that finished after its deadline.
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeout)
                throw new OperationCanceledException(linked.Token);
        }
        try
        {
            CheckActive();
            try
            {
                var payload = JsonSerializer.SerializeToElement(request, requestTypeInfo);
                CheckActive(); // A converter can reenter, navigate, cancel or dispose.
                int remaining = (int)Math.Clamp(Math.Ceiling(timeout - Stopwatch.GetElapsedTime(started).TotalMilliseconds), 1, timeout);
                message = WebViewRpcProtocol.Request(id, documentId, method, payload, remaining);
            }
            catch (WebViewRpcException) { CheckActive(); throw; }
            catch { CheckActive(); throw new WebViewRpcException(WebViewRpcErrorCategory.InvalidPayload); }
            CheckActive();
            var completion = new TaskCompletionSource<WebViewRpcProtocol.Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (sync)
            {
                if (pending.Count >= WebViewRpcProtocol.MaxPendingRequests) throw new InvalidOperationException("RPC pending request limit reached.");
                pending.Add(id, new(document, completion)); registered = true;
            }
            try
            {
                submitted = true; // The native operation may have effects even if submission later fails.
                await transport.PostMessageAsync(message, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { CheckActive(); throw new WebViewRpcException(WebViewRpcErrorCategory.Transport); }
            var reply = await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            CheckActive();
            if (reply.Error is { } code)
                throw new WebViewRpcException(code == "timeout" ? WebViewRpcErrorCategory.Timeout : WebViewRpcErrorCategory.RemoteError) { RemoteCode = code };
            TResponse? value;
            try { value = JsonSerializer.Deserialize(reply.Payload, responseTypeInfo); }
            catch { CheckActive(); throw new WebViewRpcException(WebViewRpcErrorCategory.InvalidResponse); }
            CheckActive();
            return value;
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            if ((deadline.IsCancellationRequested || Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeout) &&
                !caller.IsCancellationRequested && !document.CancellationToken.IsCancellationRequested &&
                !transportToken.IsCancellationRequested && !lifetimeToken.IsCancellationRequested)
                throw new WebViewRpcException(WebViewRpcErrorCategory.Timeout);
            throw;
        }
        finally
        {
            if (registered) { lock (sync) pending.Remove(id); }
            if (submitted && canceled) QueueCancellation(id, document, documentId);
        }
    }

    private void Receive(object? sender, WebViewTransportMessageEventArgs e)
    {
        if (disposed || e.Document.CancellationToken.IsCancellationRequested) return;
        var reply = WebViewRpcProtocol.ParseReply(e.Message);
        if (reply is null) return;
        Pending? call;
        lock (sync) pending.TryGetValue(reply.Id, out call);
        if (call is null || reply.Document != WebViewRpcProtocol.DocumentId(call.Document) ||
            call.Document.RequestId != e.Document.RequestId || call.Document.PeerGeneration != e.Document.PeerGeneration ||
            call.Document.CancellationToken.IsCancellationRequested) return;
        // An earlier observer can replace the document. Correlation must match the current
        // canonical lifetime too; Source and request IDs never upgrade caller trust.
        try
        {
            var current = transport.CurrentDocument;
            if (!current.IsReady || current.RequestId != call.Document.RequestId || current.PeerGeneration != call.Document.PeerGeneration ||
                current.CancellationToken.IsCancellationRequested) return;
        }
        catch (ObjectDisposedException) { return; }
        call.Completion.TrySetResult(reply); // First terminal response wins; duplicates cannot change it.
    }

    private void QueueCancellation(string id, WebViewDocumentContext original, string documentId)
    {
        // Completion may run off-thread. Use the existing UI dispatcher and recheck the
        // original lifetime there. Never submit an old cancel to a replacement document.
        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (transportToken.IsCancellationRequested || original.CancellationToken.IsCancellationRequested) return;
                    var current = transport.CurrentDocument;
                    if (current.RequestId != original.RequestId || current.PeerGeneration != original.PeerGeneration) return;
                    await transport.PostMessageAsync(WebViewRpcProtocol.Cancel(id, documentId), original.CancellationToken);
                }
                catch { /* Advisory only: local waits already settled, and no diagnostic contains page data. */ }
            });
        }
        catch (InvalidOperationException) { /* A custom non-native transport may have no UI dispatcher. */ }
    }

    /// <summary>Unsubscribes and cancels this client's calls on the UI thread; does not dispose the transport/browser.</summary>
    public void Dispose()
    {
        if (disposed) return;
        // Verify the borrowed transport's UI affinity while it is alive. Disposing it first
        // already revokes all calls and still permits this client to remove its subscription.
        try { if (!transportToken.IsCancellationRequested) _ = transport.CurrentDocument; }
        catch (ObjectDisposedException) { /* The owner can be disposed before its borrowed wrappers. */ }
        disposed = true; transport.MessageReceived -= Receive;
        try { lifetime.Cancel(); } finally { lifetime.Dispose(); }
    }
}
