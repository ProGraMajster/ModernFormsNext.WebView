using Android.App;
using Android.OS;
using Android.Webkit;
using AndroidX.WebKit;
using ModernFormsNext.WebView;
using ModernFormsNext.WebView.Android;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Browser = Android.Webkit.WebView;

namespace ModernFormsNext.WebView.OriginProbe;

// This standalone SDK experiment owns its Activity/View. It is not a product host,
// adapter or transport. Production NativeViewHost and both providers are unchanged.
/// <summary>Runs an isolated AndroidX SDK experiment; no product host or C# handlers are installed.</summary>
[Activity(Name = "com.programajster.modernformsnext.webview.originprobe.MainActivity", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private Probe? probe;
    /// <inheritdoc/>
    protected override async void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        try
        {
            probe = new Probe(this); SetContentView(probe.View);
            await probe.RunAsync();
            Report("ORIGIN PROBE EXECUTED; PRIVILEGED NO-GO");
            if (state is null && Intent?.GetBooleanExtra("recreate", false) == true)
            { Report("RECREATE requested after completed probe"); Recreate(); }
        }
        catch (System.OperationCanceledException) { Report("PROBE RETIRED; pending SDK waits canceled"); }
        catch (Exception error) { Report("ORIGIN PROBE FAIL: " + error.GetType().Name + ": " + error.Message); }
    }
    /// <inheritdoc/>
    protected override void OnDestroy() { try { probe?.Dispose(); probe = null; Report("DESTROY native View/listener/proxies disposed"); } finally { base.OnDestroy(); } }
    internal static void Report(string value) => global::Android.Util.Log.Info("MFNOriginProbe", value);
}

[JsonSerializable(typeof(string))]
internal partial class ProbeJson : JsonSerializerContext;

internal sealed class Probe : IDisposable
{
    private const string Origin = "https://origin-probe.mfn.invalid";
    private const string Other = "https://foreign-probe.mfn.invalid";
    private const string ObjectName = "originProbe";
    internal Browser View { get; }
    private readonly List<Packet> packets = [];
    private readonly Client client;
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private Listener? listener;
    private long epoch;
    private bool disposed;
    private TaskCompletionSource? navigation;
    private string? expected;
    private sealed record Packet(string Data, string Source, bool Main, long Epoch, JavaScriptReplyProxy Reply);

    internal Probe(Activity activity)
    {
        lifetimeToken = lifetime.Token;
        View = new Browser(activity); client = new Client(this); View.SetWebViewClient(client);
        View.Settings.JavaScriptEnabled = true;
        View.Settings.AllowFileAccess = View.Settings.AllowContentAccess = false;
        View.Settings.AllowFileAccessFromFileURLs = View.Settings.AllowUniversalAccessFromFileURLs = false;
        View.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
    }

    internal async Task RunAsync()
    {
        bool supported = WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener);
        using var provider = OperatingSystem.IsAndroidVersionAtLeast(26) ? Browser.CurrentWebViewPackage : null;
        MainActivity.Report($"PROVIDER {provider?.PackageName}/{provider?.VersionName}; SDK={(int)Build.VERSION.SdkInt}; WEB_MESSAGE_LISTENER={supported}");
        // No compatibility fallback to AddJavascriptInterface exists in this experiment.
        if (!supported) { MainActivity.Report("UNSUPPORTED; registration skipped; privileges disabled"); return; }
        Require(!Register(false), "unsupported branch never registers");
        await LoadAsync(Origin + "/top");
        Require(await EvalAsync("typeof originProbe==='undefined'") == "true", "unsupported path injects nothing");
        Require(Register(supported), "supported registration");
        await LoadAsync(Origin + "/top");
        await EvalAsync("originProbe.onmessage=()=>{window.ack=true;};originProbe.postMessage('top');null");
        await WaitAsync(() => packets.Any(p => p.Data == "top")); await WaitScriptAsync("window.ack===true");
        Require(packets.Single(p => p.Data == "top") is { Main: true, Source: Origin }, "native top-level origin/main metadata");
        MainActivity.Report("PASS exact HTTPS top-level native metadata + JavaScriptReplyProxy acknowledgement");

        await EvalAsync("window.frameDone=0;addEventListener('message',e=>{if(e.data==='done')++frameDone;});null");
        await AddFrameAsync(Origin + "/frame", null, false);
        await AddFrameAsync(Other + "/frame", null, false);
        await AddFrameAsync(Origin + ":8443/frame", null, false);
        await AddFrameAsync(null, "if(typeof originProbe!=='undefined')originProbe.postMessage('opaque');parent.postMessage('done','*');", true);
        await WaitScriptAsync("window.frameDone===4");
        await EvalAsync("originProbe.postMessage('frame-fence');null");
        await WaitAsync(() => packets.Any(p => p.Data == "frame-fence"));
        Require(packets.Any(p => p.Data == "frame-direct" && !p.Main && p.Source == Origin), "same-origin direct frame is identified");
        Require(!packets.Any(p => p.Data is "foreign" or "port" or "opaque"), "foreign/port/opaque object injection denied");
        var relay = packets.Single(p => p.Data == "parent-relay");
        Require(relay.Main && relay.Source == Origin, "same-origin frame can call parent-owned object and reports parent metadata");
        MainActivity.Report("PASS direct-frame/foreign/port/opaque matrix; parent relay reports top-level => NO-GO for original-caller authentication");

        // Reuse the real RPC JavaScript and the production Android string escaping path.
        // This is only a fixed public echo through the probe, never C# handler dispatch.
        await EvalAsync("window.modernFormsWebView={postMessage:v=>originProbe.postMessage(v)};null");
        Require(await EvalAsync(WebViewRpcProtocol.JavaScript) == "true", "existing RPC endpoint installs");
        await EvalAsync("modernFormsRpc.register('echo',x=>x);null");
        const string rpc = "{\"version\":1,\"type\":\"request\",\"id\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"document\":\"1:1\",\"method\":\"echo\",\"payload\":42,\"timeoutMs\":1000}";
        await EvalAsync(AndroidWebViewPolicy.CreateMessageScript(rpc));
        await WaitAsync(() => packets.Any(p => p.Data.StartsWith("{", StringComparison.Ordinal)));
        using (var response = JsonDocument.Parse(packets.Last(p => p.Data.StartsWith("{", StringComparison.Ordinal)).Data))
            Require(response.RootElement.GetProperty("type").GetString() == "result" && response.RootElement.GetProperty("payload").GetInt32() == 42, "existing RPC envelope round trip");
        MainActivity.Report("PASS existing RPC v1 data envelope over origin-restricted SDK object; data remains untrusted");

        var originalProxy = packets.Single(p => p.Data == "top").Reply;
        await EvalAsync("window.saved=originProbe;null");
        Remove(); int before = packets.Count;
        await EvalAsync("saved.postMessage('removed');window.fence=1;null");
        await WaitScriptAsync("window.fence===1");
        Require(Register(true), "listener recreated");
        await LoadAsync(Origin + "/replacement");
        await EvalAsync("originProbe.onmessage=()=>{window.staleReply=true;};originProbe.postMessage('replacement');null");
        await WaitAsync(() => packets.Any(p => p.Data == "replacement"));
        originalProxy.PostMessage("old-proxy");
        await EvalAsync("originProbe.postMessage('replacement-fence');null");
        await WaitAsync(() => packets.Any(p => p.Data == "replacement-fence"));
        Require(!packets.Any(p => p.Data == "removed") && packets.Count > before && await EvalAsync("window.staleReply===true") == "false", "removed callback and stale proxy do not reach replacement");
        MainActivity.Report("PASS remove/re-register/document replacement + stale reply proxy isolation");

        await LoadAsync(Origin + "/redirect");
        Require(await EvalAsync("typeof originProbe==='undefined'") == "true", "cross-origin redirect has no injected object");
        await LoadAsync(Origin + "/top");
        await ReloadAsync();
        await EvalAsync("originProbe.postMessage('reloaded');null"); await WaitAsync(() => packets.Any(p => p.Data == "reloaded"));
        View.StopLoading(); Remove(); Require(!disposed, "Stop does not own fixture");
        MainActivity.Report("PASS HTTPS redirect denial + reload + Stop/removal; privileged capability remains unavailable");
    }

    private bool Register(bool featureSupported)
    {
        if (!featureSupported || disposed) return false;
        Remove(); listener = new Listener(this, ++epoch);
        WebViewCompat.AddWebMessageListener(View, ObjectName, new[] { Origin }, listener);
        return true;
    }
    private void Remove()
    {
        if (listener is null) return;
        ++epoch; WebViewCompat.RemoveWebMessageListener(View, ObjectName); listener.Dispose(); listener = null;
    }
    private void Received(Browser view, WebMessageCompat message, global::Android.Net.Uri source, bool main, JavaScriptReplyProxy reply, long identity)
    {
        if (disposed || identity != epoch || !ReferenceEquals(view, View) || packets.Count >= 256) return;
        string? data = message.Data;
        if (data is null || data.Length > WebViewRpcProtocol.MaxMessageBytes || Encoding.UTF8.GetByteCount(data) > WebViewRpcProtocol.MaxMessageBytes) return;
        packets.Add(new(data, source.ToString() ?? "null", main, identity, reply));
        if (data == "top" && main && source.ToString() == Origin) reply.PostMessage("probe-ack");
    }
    private Task AddFrameAsync(string? uri, string? script, bool opaque) => EvalAsync(
        "(()=>{const f=document.createElement('iframe');" + (opaque ? "f.sandbox='allow-scripts';" : "") +
        (uri is null ? "f.srcdoc=" + Quote("<script>" + script + "</script>") : "f.src=" + Quote(uri)) + ";document.body.append(f);})();null");
    private async Task LoadAsync(string url)
    {
        lifetimeToken.ThrowIfCancellationRequested();
        expected = url.EndsWith("/redirect", StringComparison.Ordinal) ? Other + "/top" : url;
        navigation = new(TaskCreationOptions.RunContinuationsAsynchronously); View.LoadUrl(url);
        await navigation.Task.WaitAsync(TimeSpan.FromSeconds(10), lifetimeToken);
    }
    private async Task ReloadAsync()
    {
        lifetimeToken.ThrowIfCancellationRequested();
        navigation = new(TaskCreationOptions.RunContinuationsAsynchronously); View.Reload();
        await navigation.Task.WaitAsync(TimeSpan.FromSeconds(10), lifetimeToken);
    }
    private async Task<string> EvalAsync(string script)
    {
        lifetimeToken.ThrowIfCancellationRequested();
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = new ValueCallback(result); View.EvaluateJavascript(script, callback);
        return await result.Task.WaitAsync(TimeSpan.FromSeconds(5), lifetimeToken);
    }
    private Task WaitScriptAsync(string predicate) => WaitAsync(async () => await EvalAsync(predicate) == "true");
    private Task WaitAsync(Func<bool> predicate) => WaitAsync(() => Task.FromResult(predicate()));
    private async Task WaitAsync(Func<Task<bool>> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!await predicate()) await Task.Delay(20, timeout.Token);
    }
    private static string Quote(string value) => JsonSerializer.Serialize(value, ProbeJson.Default.String);
    private static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { lifetime.Cancel(); Remove(); }
        finally
        {
            try { View.SetWebViewClient(null!); View.StopLoading(); }
            finally
            {
                try
                {
                    if (View.Parent is global::Android.Views.ViewGroup parent) parent.RemoveView(View);
                    View.Destroy();
                }
                finally
                {
                    View.Dispose(); client.Dispose();
                    // JNI wrappers override GetHashCode/Equals using the live Java peer.
                    // Compare managed identity and materialize before disposing any wrapper;
                    // lazy Distinct would hash an already disposed repeated reply proxy.
                    var replies = packets.Select(p => p.Reply).Distinct<JavaScriptReplyProxy>(ReferenceEqualityComparer.Instance).ToArray();
                    foreach (var reply in replies) reply.Dispose(); packets.Clear();
                    lifetime.Dispose();
                }
            }
        }
    }
    private sealed class Listener(Probe owner, long identity) : Java.Lang.Object, WebViewCompat.IWebMessageListener
    {
        public void OnPostMessage(Browser? view, WebMessageCompat? message, global::Android.Net.Uri? sourceOrigin, bool isMainFrame, JavaScriptReplyProxy? replyProxy)
        {
            if (view is not null && message is not null && sourceOrigin is not null && replyProxy is not null)
                owner.Received(view, message, sourceOrigin, isMainFrame, replyProxy, identity);
        }
    }
    private sealed class ValueCallback(TaskCompletionSource<string> result) : Java.Lang.Object, IValueCallback
    { public void OnReceiveValue(Java.Lang.Object? value) => result.TrySetResult(value?.ToString() ?? "null"); }
    private sealed class Client(Probe owner) : WebViewClient
    {
        public override void OnPageFinished(Browser? view, string? url) { if (!owner.disposed && url == owner.expected) owner.navigation?.TrySetResult(); }
        public override WebResourceResponse? ShouldInterceptRequest(Browser? view, IWebResourceRequest? request)
        {
            if (!Uri.TryCreate(request?.Url?.ToString(), UriKind.Absolute, out var uri) ||
                uri.Host is not ("origin-probe.mfn.invalid" or "foreign-probe.mfn.invalid")) return null;
            string html = uri.AbsolutePath == "/redirect" ? "<meta http-equiv='refresh' content='0;url=" + Other + "/top'>" :
                uri.AbsolutePath == "/frame" ? "<script>if(typeof originProbe!=='undefined'){originProbe.postMessage('frame-direct');parent.originProbe.postMessage('parent-relay');}parent.postMessage('done','*');</script>" : "<h1>Public SDK origin fixture</h1>";
            if (uri.Host == "foreign-probe.mfn.invalid" && uri.AbsolutePath == "/frame")
                html = "<script>if(typeof originProbe!=='undefined')originProbe.postMessage('foreign');parent.postMessage('done','*');</script>";
            if (!uri.IsDefaultPort) html = "<script>if(typeof originProbe!=='undefined')originProbe.postMessage('port');parent.postMessage('done','*');</script>";
            return new WebResourceResponse("text/html", "UTF-8", new MemoryStream(Encoding.UTF8.GetBytes(html)));
        }
        public override void OnReceivedSslError(Browser? view, SslErrorHandler? handler, global::Android.Net.Http.SslError? error) => handler?.Cancel();
    }
}
