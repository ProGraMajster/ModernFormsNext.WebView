using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WebView.Tests;

internal sealed class FakeProvider : IWebViewPeerFactoryProvider
{
    public readonly List<FakePeer> Peers = [];
    public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
    {
        var peer = new FakePeer(site, client); Peers.Add(peer); return peer;
    }
}

internal class FakePeer(INativeViewSite site, IWebViewPeerClient client) : IWebViewPeer, IWebViewLocalContentPeer
{
    public bool SupportsLocalContent { get; set; }
    public IWebViewPeerClient Client { get; } = client;
    public INativeViewSite Site { get; } = site;
    public WebViewSettings? Settings;
    public readonly List<WebViewNavigationRequest> Requests = [];
    public long Id;
    public int Disposals, Stops, Reloads;
    public readonly TaskCompletionSource<string> Script = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string? Message;
    public Action<CancellationToken>? ScriptStarted;
    public PixelSize Size;
    public void Initialize(WebViewSettings settings) => Settings = settings;
    public void Ready() => Client.Initialized("Fake", "Deterministic", "1");
    public void ApplySettings(WebViewSettings settings) => Settings = settings;
    public void Navigate(WebViewNavigationRequest request)
    {
        Requests.Add(request); Id = request.Id;
        Client.NavigationStarting(Id, request.Uri, false);
    }
    public void GoBack(long id) { Id = id; Client.NavigationStarting(id, new Uri("https://back.test"), false); }
    public void GoForward(long id) { Id = id; Client.NavigationStarting(id, new Uri("https://forward.test"), false); }
    public void Reload(long id) { Id = id; ++Reloads; Client.NavigationStarting(id, null, false); }
    public void Stop() => ++Stops;
    public Task<string> ExecuteScriptAsync(string script, CancellationToken token)
    { ScriptStarted?.Invoke(token); return Script.Task; }
    public Task PostMessageAsync(string message, CancellationToken token) { Message = message; return Task.CompletedTask; }
    public void Resize(PixelSize size) => Size = size;
    public void RequestFocus() { }
    public bool TryMoveFocus(bool forward) => false;
    public void Dispose() => ++Disposals;
}
