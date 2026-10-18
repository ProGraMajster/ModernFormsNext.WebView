using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.WorldProbe;

// A diagnostic counter, never an operation dispatcher. The captured context is the
// existing transport's document token, not a second lifecycle or document counter.
internal sealed class WorldSession : IAsyncDisposable
{
    private readonly ResearchPeer peer;
    private readonly IWebViewTransport transport;
    private readonly WebViewDocumentContext document;
    private readonly CoreWebView2DevToolsProtocolEventReceiver receiver;
    private bool disposed;
    internal string Name { get; } = "__mfn_probe_" + Guid.NewGuid().ToString("N");
    internal int ContextId { get; private set; }
    internal int Accepted { get; private set; }
    internal int Rejected { get; private set; }
    internal string? LastPacket { get; private set; }
    internal readonly HashSet<string> Payloads = [];

    private WorldSession(ResearchPeer peer, IWebViewTransport transport)
    {
        this.peer = peer; this.transport = transport; document = transport.CurrentDocument;
        Program.Require(document.IsReady && !document.CancellationToken.IsCancellationRequested, "ready canonical document");
        receiver = peer.Core.GetDevToolsProtocolEventReceiver("Runtime.bindingCalled");
        receiver.DevToolsProtocolEventReceived += Received;
    }
    internal static async Task<WorldSession> CreateAsync(ResearchPeer peer, IWebViewTransport transport)
    {
        var session = new WorldSession(peer, transport);
        try
        {
            await session.CallAsync("Page.enable", new { });
            await session.CallAsync("Runtime.enable", new { });
            var tree = await session.CallAsync("Page.getFrameTree", new { });
            string frameId = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;
            // Never grant universal access, setBypassCSP or a remote debugging port.
            // These fixtures intentionally have no CSP. World CSP semantics are NOT
            // qualified and this cannot be installed into privileged application content.
            var world = await session.CallAsync("Page.createIsolatedWorld", new { frameId, worldName = session.Name, grantUniveralAccess = false });
            session.ContextId = world.GetProperty("executionContextId").GetInt32();
            // A named world avoids the deprecated addBinding executionContextId option.
            // Events must still match the exact native context ID plus canonical token.
            await session.CallAsync("Runtime.addBinding", new { name = session.Name, executionContextName = session.Name });
            await session.EvaluateAsync(session.Name + "('positive')");
            await Program.WaitAsync(() => session.Payloads.Contains("positive"));
            return session;
        }
        catch { await session.DisposeAsync(); throw; }
    }
    internal async Task<JsonElement> CallAsync(string method, object args)
    {
        Dispatcher.UIThread.VerifyAccess();
        string result = await peer.Core.CallDevToolsProtocolMethodAsync(method, JsonSerializer.Serialize(args));
        using var json = JsonDocument.Parse(result); return json.RootElement.Clone();
    }
    internal async Task EvaluateAsync(string expression)
    {
        var result = await CallAsync("Runtime.evaluate", new { expression, contextId = ContextId, returnByValue = true });
        if (result.TryGetProperty("exceptionDetails", out _)) throw new InvalidOperationException("Isolated-world evaluation failed.");
    }
    private void Received(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e) => Observe(e.ParameterObjectAsJson);
    // Replay is explicitly a recorded-packet guard test, not a synthetic proof that
    // a native queued callback executed. Live DOM/binding calls are tested separately.
    internal void Observe(string packet)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (packet.Length > 2048) return;
        using var json = JsonDocument.Parse(packet);
        var data = json.RootElement;
        if (data.GetProperty("name").GetString() != Name) return;
        string? payload = data.GetProperty("payload").GetString();
        if (disposed || !peer.Current || transport.LifetimeToken.IsCancellationRequested ||
            document.CancellationToken.IsCancellationRequested || data.GetProperty("executionContextId").GetInt32() != ContextId ||
            transport.CurrentDocument != document || payload is null || payload.Length > 128)
        { Rejected = Math.Min(256, Rejected + 1); return; }
        LastPacket = packet;
        Accepted = Math.Min(256, Accepted + 1);
        if (Payloads.Count < 16) Payloads.Add(payload);
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; receiver.DevToolsProtocolEventReceived -= Received;
        if (peer.Current) await CallAsync("Runtime.removeBinding", new { name = Name });
    }
}
