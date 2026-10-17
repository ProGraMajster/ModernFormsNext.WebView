using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ModernFormsNext.WebView.Tests;

internal sealed record RpcData(int Answer, string Text);
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(RpcData))]
[JsonSerializable(typeof(JsonElement))]
internal partial class RpcTestJson : JsonSerializerContext;

public sealed class RpcTests
{
    private sealed class Transport : IWebViewTransport
    {
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenSource document = new();
        private bool disposed;
        internal readonly List<JsonObject> Sent = [];
        internal Action<JsonObject>? OnSend;
        internal Task Submission = Task.CompletedTask;
        public WebViewDocumentContext CurrentDocument { get; private set; }
        public CancellationToken LifetimeToken { get; }
        public event EventHandler<WebViewTransportMessageEventArgs>? MessageReceived;
        internal int Subscribers => MessageReceived?.GetInvocationList().Length ?? 0;
        internal Transport() { LifetimeToken = lifetime.Token; CurrentDocument = new(1, 1, document.Token, true); }
        public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default) => Task.FromResult("null");
        public Task PostMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = JsonNode.Parse(message)!.AsObject(); Sent.Add(value); OnSend?.Invoke(value); return Submission;
        }
        internal void Receive(string message, WebViewDocumentContext? original = null) =>
            MessageReceived?.Invoke(this, new(message, new Uri("https://untrusted.test/"), original ?? CurrentDocument));
        internal void Reply(JsonObject request, string payload = "42") => Receive(Result(request, payload).ToJsonString());
        internal void Replace()
        {
            document.Cancel(); document.Dispose(); document = new();
            CurrentDocument = new(CurrentDocument.RequestId + 1, CurrentDocument.PeerGeneration + 1, document.Token, true);
        }
        internal void NotReady() => CurrentDocument = CurrentDocument with { IsReady = false };
        public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); document.Cancel(); lifetime.Dispose(); document.Dispose(); }
    }
    private static JsonObject Result(JsonObject request, string payload = "42") => new()
    {
        ["version"] = 1, ["type"] = "result", ["id"] = request["id"]!.GetValue<string>(),
        ["document"] = request["document"]!.GetValue<string>(), ["payload"] = JsonNode.Parse(payload)
    };
    private static Task<int> Invoke(WebViewRpcClient client, TimeSpan? timeout = null, CancellationToken token = default) =>
        client.InvokeAsync("echo", 7, RpcTestJson.Default.Int32, RpcTestJson.Default.Int32, timeout, token);

    [Fact]
    public async Task TypedRoundTripAndSynchronousReplyUseExplicitMetadata()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        t.OnSend = r => t.Reply(r, r["payload"]!.ToJsonString());
        var value = new RpcData(42, "Zażółć 😀 \" \\ </script>");
        Assert.Equal(value, await client.InvokeAsync("echo", value, RpcTestJson.Default.RpcData, RpcTestJson.Default.RpcData));
        Assert.Equal(1, t.Sent[0]["version"]!.GetValue<int>()); Assert.Equal("1:1", t.Sent[0]["document"]!.GetValue<string>());
        Assert.Equal(32, t.Sent[0]["id"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task OutOfOrderAndDuplicateRepliesSettleOnlyMatchingCallOnce()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var first = Invoke(client); var second = Invoke(client);
        Assert.NotEqual(t.Sent[0]["id"]!.GetValue<string>(), t.Sent[1]["id"]!.GetValue<string>());
        t.Reply(t.Sent[1], "2"); Assert.False(first.IsCompleted);
        t.Reply(t.Sent[1], "999"); t.Reply(t.Sent[0], "1");
        Assert.Equal(1, await first); Assert.Equal(2, await second);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("id")]
    [InlineData("document")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("duplicate-escaped")]
    [InlineData("shape")]
    [InlineData("type")]
    [InlineData("malformed")]
    [InlineData("size")]
    [InlineData("utf8-size")]
    [InlineData("depth")]
    [InlineData("request")]
    [InlineData("error-code")]
    [InlineData("error-detail")]
    public async Task InvalidAndUnsolicitedDataCannotCompletePendingRequest(string variant)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var task = Invoke(client); var response = Result(t.Sent[0]); string? raw = null;
        switch (variant)
        {
            case "version": response["version"] = 2; break;
            case "id": response["id"] = new string('a', 32); break;
            case "document": response["document"] = "1:2"; break;
            case "unknown": response["extra"] = "ignored?"; break;
            case "duplicate": raw = response.ToJsonString().Insert(1, "\"version\":1,"); break;
            case "duplicate-escaped": raw = response.ToJsonString().Insert(1, "\"\\u0076ersion\":1,"); break;
            case "shape": raw = "[]"; break;
            case "type": response["version"] = "1"; break;
            case "malformed": raw = "{\""; break;
            case "size": raw = new string('x', WebViewRpcProtocol.MaxMessageBytes + 1); break;
            case "utf8-size":
                response["payload"] = new string('界', 24000);
                raw = response.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                Assert.InRange(raw.Length, 1, WebViewRpcProtocol.MaxMessageBytes); break;
            case "depth": raw = response.ToJsonString().Replace("42", new string('[', 40) + "0" + new string(']', 40), StringComparison.Ordinal); break;
            case "request": response["type"] = "request"; break;
            case "error-code": response.Remove("payload"); response["type"] = "error"; response["error"] = new JsonObject { ["code"] = "secret exception" }; break;
            case "error-detail": response.Remove("payload"); response["type"] = "error"; response["error"] = new JsonObject { ["code"] = "handler_error", ["message"] = "secret" }; break;
        }
        t.Receive(raw ?? response.ToJsonString()); Assert.False(task.IsCompleted);
        t.Reply(t.Sent[0]); Assert.Equal(42, await task);
    }

    [Fact]
    public async Task StaleEventContextAndCanonicalReplacementAreRejected()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var old = t.CurrentDocument; var task = Invoke(client);
        t.Receive(Result(t.Sent[0]).ToJsonString(), old with { RequestId = 100 }); Assert.False(task.IsCompleted);
        t.Replace(); t.Receive(Result(t.Sent[0]).ToJsonString(), old);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        var next = Invoke(client); t.Reply(t.Sent[0]); Assert.False(next.IsCompleted);
        t.Reply(t.Sent[1]); Assert.Equal(42, await next);
    }

    [Fact]
    public async Task InvalidTypedResultAndRemoteErrorsAreSanitized()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var typed = Invoke(client); t.Reply(t.Sent[0], "\"secret-payload\"");
        var failure = await Assert.ThrowsAsync<WebViewRpcException>(() => typed);
        Assert.Equal(WebViewRpcErrorCategory.InvalidResponse, failure.Category); Assert.Null(failure.InnerException);
        Assert.DoesNotContain("secret", failure.ToString());
        var remote = Invoke(client); var packet = Result(t.Sent[1]); packet.Remove("payload");
        packet["type"] = "error"; packet["error"] = new JsonObject { ["code"] = "handler_error" }; t.Receive(packet.ToJsonString());
        failure = await Assert.ThrowsAsync<WebViewRpcException>(() => remote);
        Assert.Equal(WebViewRpcErrorCategory.RemoteError, failure.Category); Assert.Equal("handler_error", failure.RemoteCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineBoundsBothSubmissionAndReply(bool blockSubmission)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        if (blockSubmission) t.Submission = new TaskCompletionSource().Task;
        var failure = await Assert.ThrowsAsync<WebViewRpcException>(() => Invoke(client, TimeSpan.FromMilliseconds(30)));
        Assert.Equal(WebViewRpcErrorCategory.Timeout, failure.Category);
    }

    [Theory]
    [InlineData("caller")]
    [InlineData("client")]
    [InlineData("transport")]
    [InlineData("document")]
    public async Task EveryLifetimeCancelsPendingWaitWithoutNativeReply(string lifetime)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t); using var caller = new CancellationTokenSource();
        var task = Invoke(client, token: caller.Token);
        switch (lifetime) { case "caller": caller.Cancel(); break; case "client": client.Dispose(); break; case "transport": t.Dispose(); break; default: t.Replace(); break; }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task PreCancellationAndSizeLimitsPreventSubmission()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Invoke(client, token: new CancellationToken(true)));
        var failure = await Assert.ThrowsAsync<WebViewRpcException>(() => client.InvokeAsync("echo", new string('x', 70000), RpcTestJson.Default.String, RpcTestJson.Default.String));
        Assert.Equal(WebViewRpcErrorCategory.InvalidPayload, failure.Category); Assert.Empty(t.Sent);
    }

    [Fact]
    public async Task PendingLimitIsBoundedAndCapacityReturnsAfterCompletion()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var calls = Enumerable.Range(0, WebViewRpcProtocol.MaxPendingRequests).Select(_ => Invoke(client)).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke(client)); Assert.Equal(64, t.Sent.Count);
        foreach (var request in t.Sent.ToArray()) t.Reply(request);
        await Task.WhenAll(calls);
        var next = Invoke(client); t.Reply(t.Sent[^1]); Assert.Equal(42, await next);
    }

    [Fact]
    public async Task TransportFailureDoesNotLeakDetailsAndClientBorrowsTransport()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        t.OnSend = _ => throw new Exception("secret-native-detail");
        var error = await Assert.ThrowsAsync<WebViewRpcException>(() => Invoke(client));
        Assert.Equal(WebViewRpcErrorCategory.Transport, error.Category); Assert.DoesNotContain("secret", error.ToString());
        client.Dispose(); Assert.False(t.LifetimeToken.IsCancellationRequested);
        Assert.Equal(0, t.Subscribers);
        Assert.Throws<ObjectDisposedException>(() => { _ = Invoke(client); });
    }

    [Theory]
    [InlineData("")]
    [InlineData("method with spaces")]
    [InlineData("ą")]
    public void InvalidMethodsAreRejectedBeforeSubmission(string method)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        Assert.Throws<ArgumentException>(() => { _ = client.InvokeAsync(method, 1, RpcTestJson.Default.Int32, RpcTestJson.Default.Int32); }); Assert.Empty(t.Sent);
    }

    [Fact]
    public void ReadinessAndTimeoutPolicyAreExplicit()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Invoke(client, TimeSpan.Zero); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Invoke(client, TimeSpan.FromSeconds(121)); });
        t.NotReady(); Assert.Throws<InvalidOperationException>(() => { _ = Invoke(client); }); Assert.Empty(t.Sent);
    }

    [Fact]
    public async Task ExactWireSizeBoundaryIsAcceptedAndOneExtraByteIsIgnored()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        var first = client.InvokeAsync("echo", "", RpcTestJson.Default.String, RpcTestJson.Default.String);
        var response = Result(t.Sent[0], "\"\""); int length = WebViewRpcProtocol.MaxMessageBytes - response.ToJsonString().Length;
        response["payload"] = new string('x', length); string raw = response.ToJsonString();
        Assert.Equal(WebViewRpcProtocol.MaxMessageBytes, raw.Length); t.Receive(raw);
        Assert.Equal(length, (await first)!.Length);
        var second = client.InvokeAsync("echo", "", RpcTestJson.Default.String, RpcTestJson.Default.String);
        response = Result(t.Sent[1], "\"\""); response["payload"] = new string('x', length + 1);
        t.Receive(response.ToJsonString()); Assert.False(second.IsCompleted);
        t.Reply(t.Sent[1], "\"valid\""); Assert.Equal("valid", await second);
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(32, false)]
    public async Task OutgoingDepthIncludesEnvelope(int arrays, bool valid)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t);
        using var data = JsonDocument.Parse(new string('[', arrays) + "0" + new string(']', arrays));
        t.OnSend = r => t.Reply(r);
        var task = client.InvokeAsync("echo", data.RootElement, RpcTestJson.Default.JsonElement, RpcTestJson.Default.Int32);
        if (valid) Assert.Equal(42, await task);
        else
        {
            var error = await Assert.ThrowsAsync<WebViewRpcException>(() => task);
            Assert.Equal(WebViewRpcErrorCategory.InvalidPayload, error.Category); Assert.Empty(t.Sent);
        }
    }

    private sealed class CancelOnRead(Action cancel) : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { cancel(); return reader.GetInt32(); }
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringApplicationConverterCannotReturnObsoleteResult(bool throws)
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t); using var caller = new CancellationTokenSource();
        var metadata = new RpcTestJson(new JsonSerializerOptions { Converters = { new CancelOnRead(() =>
        { caller.Cancel(); if (throws) throw new InvalidOperationException("private-converter-detail"); }) } });
        var task = client.InvokeAsync("echo", 1, RpcTestJson.Default.Int32, metadata.Int32, cancellationToken: caller.Token);
        t.Reply(t.Sent[0]); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private sealed class CancelOnWrite(Action cancel) : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetInt32();
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) { cancel(); throw new InvalidOperationException("private-converter-detail"); }
    }

    [Fact]
    public async Task CancellationDuringRequestSerializationPreventsSubmission()
    {
        using var t = new Transport(); using var client = new WebViewRpcClient(t); using var caller = new CancellationTokenSource();
        var metadata = new RpcTestJson(new JsonSerializerOptions { Converters = { new CancelOnWrite(caller.Cancel) } });
        var task = client.InvokeAsync("echo", 1, metadata.Int32, RpcTestJson.Default.Int32, cancellationToken: caller.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); Assert.Empty(t.Sent);
    }
}
