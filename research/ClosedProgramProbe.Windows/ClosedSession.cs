using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.WorldProbe;

// Research only. Authority flows from a finite native harness action, through an
// exact permit and a closed literal program, to this SDK receiver. Page messages
// never enter this receiver or grant permits. Labels/nonces alone are not authority.
internal sealed class ClosedSession : IAsyncDisposable
{
    internal const string Binding = "__mfnClosedCounterBinding";
    internal const string World = "mfn-closed-counter-research";
    private readonly ResearchPeer peer;
    private readonly CoreWebView2 core; // Captured solely for real stale-SDK negative attempts.
    private readonly IWebViewTransport transport;
    private readonly WebViewDocumentContext document;
    private readonly OwnedCounter counter;
    private readonly Dictionary<int, NativeContext> contexts = [];
    private readonly CoreWebView2DevToolsProtocolEventReceiver binding, created, destroyed, cleared;
    private readonly CancellationTokenRegistration cancellation;
    private Permit? permit;
    private NativeContext context = null!;
    private string mainFrame = "";
    private bool disposed;
    internal int LiveCalls { get; private set; }
    internal string LastReason { get; private set; } = "None";
    internal string? LastPacket { get; private set; }
    internal int ContextId => context.Id;
    internal string ContextUniqueId => context.UniqueId;
    internal WebViewDocumentContext Document => document;

    private sealed record NativeContext(int Id, string UniqueId, string FrameId, bool IsDefault, string Type);
    private sealed record Permit(string Payload, NativeContext Context, long Deadline);

    private ClosedSession(ResearchPeer peer, IWebViewTransport transport, OwnedCounter counter)
    {
        this.peer = peer; this.transport = transport; this.counter = counter;
        core = peer.Core; document = transport.CurrentDocument;
        Program.Require(document.IsReady && !document.CancellationToken.IsCancellationRequested, "ready canonical document");
        binding = core.GetDevToolsProtocolEventReceiver("Runtime.bindingCalled");
        created = core.GetDevToolsProtocolEventReceiver("Runtime.executionContextCreated");
        destroyed = core.GetDevToolsProtocolEventReceiver("Runtime.executionContextDestroyed");
        cleared = core.GetDevToolsProtocolEventReceiver("Runtime.executionContextsCleared");
        binding.DevToolsProtocolEventReceived += Received;
        created.DevToolsProtocolEventReceived += Created;
        destroyed.DevToolsProtocolEventReceived += Destroyed;
        cleared.DevToolsProtocolEventReceived += Cleared;
        // Cancellation is canonical and happens before SDK Stop/retirement. Clearing
        // this reference cannot grant anything, including from a cancellation observer.
        cancellation = document.CancellationToken.Register(() => Interlocked.Exchange(ref permit, null));
    }

    internal static async Task<ClosedSession> CreateAsync(ResearchPeer peer, IWebViewTransport transport, OwnedCounter counter)
    {
        var session = new ClosedSession(peer, transport, counter);
        try
        {
            await session.CallAsync("Page.enable", new { });
            await session.CallAsync("Runtime.enable", new { });
            var tree = await session.CallAsync("Page.getFrameTree", new { });
            session.mainFrame = tree.GetProperty("frameTree").GetProperty("frame").GetProperty("id").GetString()!;
            var world = await session.CallAsync("Page.createIsolatedWorld", new { frameId = session.mainFrame, worldName = World, grantUniveralAccess = false });
            int id = world.GetProperty("executionContextId").GetInt32();
            await Program.WaitAsync(() => session.contexts.ContainsKey(id));
            session.context = session.contexts[id];
            Program.Require(session.context.UniqueId.Length > 0 && !session.context.IsDefault && session.context.Type == "isolated" &&
                session.context.FrameId == session.mainFrame, "native unique isolated main-frame metadata; missing metadata is NO-GO");
            // Probe actual uniqueContextId support. Ignoring this experimental field
            // and evaluating in main world must never silently qualify a provider.
            bool invalidRejected = false;
            try { await session.CallAsync("Runtime.evaluate", new { expression = "void 0", uniqueContextId = "mfn-invalid-" + Guid.NewGuid().ToString("N") }); }
            catch (ArgumentException) { invalidRejected = true; }
            catch (System.Runtime.InteropServices.COMException) { invalidRejected = true; }
            Program.Require(invalidRejected, "SDK rejects invalid uniqueContextId; no main-world fallback");
            await session.CallAsync("Runtime.addBinding", new { name = Binding, executionContextName = World });
            int calls = session.LiveCalls;
            await session.EvaluatePayloadAsync("capability-probe");
            await Program.WaitAsync(() => session.LiveCalls > calls);
            Program.Require(session.LastReason == "NoPermit", "genuine correct-world callback without permit is denied");
            Console.WriteLine($"CONTEXT native id={id} unique={session.context.UniqueId} mainFrame={session.mainFrame} type=isolated default=false");
            return session;
        }
        catch { await session.DisposeAsync(); throw; }
    }

    private void Created(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        using var packet = JsonDocument.Parse(e.ParameterObjectAsJson);
        var c = packet.RootElement.GetProperty("context");
        if (!c.TryGetProperty("uniqueId", out var unique) || !c.TryGetProperty("auxData", out var aux) ||
            !aux.TryGetProperty("frameId", out var frame) || !aux.TryGetProperty("isDefault", out var isDefault) ||
            !aux.TryGetProperty("type", out var type)) return;
        int id = c.GetProperty("id").GetInt32();
        if (contexts.Count >= 64 && !contexts.ContainsKey(id)) { Interlocked.Exchange(ref permit, null); return; }
        contexts[id] = new(id, unique.GetString() ?? "", frame.GetString() ?? "", isDefault.GetBoolean(), type.GetString() ?? "");
    }
    private void Destroyed(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        using var packet = JsonDocument.Parse(e.ParameterObjectAsJson);
        int id = packet.RootElement.GetProperty("executionContextId").GetInt32();
        contexts.Remove(id);
        if (context?.Id == id) Interlocked.Exchange(ref permit, null);
    }
    private void Cleared(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    { contexts.Clear(); Interlocked.Exchange(ref permit, null); }

    // The harness has a single fixed diagnostic intent. No method/argument selected
    // by a page, DOM event, transport packet, callback, service or reflection flows here.
    internal string GrantCounterIncrement(bool shortExpiry = false)
    {
        Dispatcher.UIThread.VerifyAccess();
        Program.Require(CurrentReason() is null, "native grant requires the exact current peer/document/world");
        string payload = "IncrementOwnedCounter|delta=1|" + Guid.NewGuid().ToString("N");
        long ticks = shortExpiry ? Stopwatch.Frequency / 25 : Stopwatch.Frequency * 10;
        permit = new(payload, context, Stopwatch.GetTimestamp() + ticks);
        return payload;
    }
    private string? CurrentReason()
    {
        if (disposed) return "Disposed";
        if (!peer.Current || transport.LifetimeToken.IsCancellationRequested || document.CancellationToken.IsCancellationRequested) return "CanonicalCanceled";
        if (transport.CurrentDocument != document) return "DocumentChanged";
        if (!contexts.TryGetValue(context.Id, out var value) || value != context || value.FrameId != mainFrame || value.IsDefault || value.Type != "isolated") return "NativeContextRetired";
        return null;
    }
    private string? Guard(JsonElement packet)
    {
        string? retired = CurrentReason();
        if (retired is not null) return retired;
        if (packet.GetProperty("executionContextId").GetInt32() != context.Id) return "WrongNativeContext";
        var granted = permit;
        if (granted is null) return "NoPermit";
        if (granted.Context != context) return "PermitContextChanged";
        if (Stopwatch.GetTimestamp() >= granted.Deadline) { Interlocked.Exchange(ref permit, null); return "Expired"; }
        return string.Equals(packet.GetProperty("payload").GetString(), granted.Payload, StringComparison.Ordinal) ? null : "WrongOperationOrArguments";
    }
    private void Received(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        Dispatcher.UIThread.VerifyAccess();
        string packet = e.ParameterObjectAsJson;
        if (packet.Length > 2048) { Interlocked.Exchange(ref permit, null); return; }
        using var json = JsonDocument.Parse(packet);
        if (json.RootElement.GetProperty("name").GetString() != Binding) return;
        Program.Require(LiveCalls < 256, "bounded native callback fixture");
        LiveCalls++; LastPacket = packet;
        LastReason = Guard(json.RootElement) ?? "Accepted";
        if (LastReason == "Accepted")
        {
            // Clear BEFORE effect. This is the ONLY counter mutation call site.
            Program.Require(Interlocked.Exchange(ref permit, null) is not null, "one-use native permit");
            counter.IncrementOne();
        }
        Console.WriteLine($"LIVE SDK callback #{LiveCalls} decision={LastReason} counter={counter.Value}");
    }
    internal string InspectRecorded(string packet)
    {
        // Deliberately cannot increment or consume a permit. This is recorded-data
        // inspection, never routed into Received and never reported as live delivery.
        using var json = JsonDocument.Parse(packet);
        string reason = Guard(json.RootElement) ?? "WouldMatchButRecordedDataHasNoAuthority";
        Console.WriteLine("RECORDED inspection only decision=" + reason + " counter=" + counter.Value);
        return reason;
    }
    internal async Task<JsonElement> CallAsync(string method, object args)
    {
        Dispatcher.UIThread.VerifyAccess();
        string result = await core.CallDevToolsProtocolMethodAsync(method, JsonSerializer.Serialize(args)).WaitAsync(TimeSpan.FromSeconds(10));
        using var json = JsonDocument.Parse(result); return json.RootElement.Clone();
    }
    internal async Task EvaluatePayloadAsync(string payload, string? uniqueContext = null)
    {
        // Whole authorized JS call graph: a bare binding call and one C# serialized
        // primitive literal. No DOM/global object/property/JSON/prototype/page callback.
        string expression = "void " + Binding + "(" + JsonSerializer.Serialize(payload) + ")";
        var result = await CallAsync("Runtime.evaluate", new { expression, uniqueContextId = uniqueContext ?? context.UniqueId, returnByValue = true });
        if (result.TryGetProperty("exceptionDetails", out _)) throw new InvalidOperationException("Actual isolated-world SDK execution refused: " + result.GetProperty("exceptionDetails"));
    }
    internal async Task<string> CreateChildWorldAsync()
    {
        var tree = await CallAsync("Page.getFrameTree", new { });
        string frameId = tree.GetProperty("frameTree").GetProperty("childFrames")[0].GetProperty("frame").GetProperty("id").GetString()!;
        var world = await CallAsync("Page.createIsolatedWorld", new { frameId, worldName = World, grantUniveralAccess = false });
        int id = world.GetProperty("executionContextId").GetInt32();
        await Program.WaitAsync(() => contexts.ContainsKey(id));
        Program.Require(id != context.Id && contexts[id].FrameId != mainFrame, "actual separate native child-frame world");
        return contexts[id].UniqueId;
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; Interlocked.Exchange(ref permit, null); cancellation.Dispose();
        binding.DevToolsProtocolEventReceived -= Received; created.DevToolsProtocolEventReceived -= Created;
        destroyed.DevToolsProtocolEventReceived -= Destroyed; cleared.DevToolsProtocolEventReceived -= Cleared;
        // A retired native controller can no longer remove a binding; a fresh peer
        // has a separate Core/channel. Never touch that replacement controller here.
        if (peer.Current)
        {
            await CallAsync("Runtime.removeBinding", new { name = Binding });
            await CallAsync("Runtime.disable", new { }); // Next session receives native descriptions afresh.
        }
    }
}

internal sealed class OwnedCounter
{
    internal int Value { get; private set; }
    internal void IncrementOne() { Program.Require(Value < 128, "bounded diagnostic counter"); Value++; }
}
