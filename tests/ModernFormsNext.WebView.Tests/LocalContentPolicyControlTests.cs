using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WebView.Tests;

public sealed class LocalContentPolicyControlTests
{
    private sealed class PolicyPeer(INativeViewSite site, IWebViewPeerClient client) : FakePeer(site, client), IWebViewLocalContentPolicyPeer
    { public bool SupportsLocalContentPolicy { get; set; } = true; }
    private sealed class Provider : IWebViewPeerFactoryProvider
    {
        internal readonly List<PolicyPeer> Peers = [];
        internal bool Supported = true;
        public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
        { var peer = new PolicyPeer(site, client) { SupportsLocalContent = true, SupportsLocalContentPolicy = Supported }; Peers.Add(peer); return peer; }
    }
    private static WebViewLocalContent Content(WebViewLocalContentPolicy policy) => new(new Dictionary<string, byte[]> { ["index.html"] = [] }, policy);

    [Fact]
    public void LegacyPeerLoadsDefaultEquivalentButRejectsOptInBeforeInvalidatingCurrentDocument()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.SupportsLocalContent = true; peer.Ready();
        browser.NavigateToLocalContent(Content(new WebViewLocalContentPolicy())); peer.Client.NavigationCompleted(peer.Id, browser.Source, 200);
        using var transport = new WebViewTransport(browser); var before = transport.CurrentDocument; int count = peer.Requests.Count;
        Assert.Throws<NotSupportedException>(() => browser.NavigateToLocalContent(Content(WebViewLocalContentPolicy.Application)));
        Assert.Equal(count, peer.Requests.Count); Assert.Equal(before.RequestId, transport.CurrentDocument.RequestId);
        Assert.False(before.CancellationToken.IsCancellationRequested);
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.ConfigurableLocalContent));
    }

    [Fact]
    public void QueuedPolicyMismatchDoesNotSubmitAndCannotReloadUntilAQualifiedPeerExists()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var provider = new Provider { Supported = false };
        var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100) });
        var content = Content(WebViewLocalContentPolicy.Application); browser.NavigateToLocalContent(content);
        using var window = app.Show(form); WebViewErrorCategory? failure = null; browser.NavigationFailed += (_, e) => failure = e.Category;
        var peer = provider.Peers.Single(); peer.Ready(); Assert.Equal(WebViewErrorCategory.Engine, failure);
        Assert.Empty(peer.Requests); Assert.False(browser.Reload()); Assert.False(browser.IsJavaScriptEnabled);
        Assert.True(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.LocalContent));
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.ConfigurableLocalContent));
        provider.Supported = true; window.NativeViews.SetAvailable(false); window.NativeViews.SetAvailable(true);
        var replacement = provider.Peers.Last(); replacement.Ready(); Assert.Same(content, Assert.Single(replacement.Requests).LocalContent);
    }

    [Fact]
    public void ReplacementAndRetiredCallbacksCannotRestoreTheOldPolicyOrAuthenticateMessages()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var provider = new Provider();
        var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        var first = Content(WebViewLocalContentPolicy.Application); browser.NavigateToLocalContent(first);
        long oldId = peer.Id; peer.Client.NavigationCompleted(oldId, browser.Source, 200);
        using var transport = new WebViewTransport(browser); var old = transport.CurrentDocument;
        var second = Content(WebViewLocalContentPolicy.Default); browser.NavigateToLocalContent(second);
        Assert.True(old.CancellationToken.IsCancellationRequested);
        peer.Client.NavigationCompleted(oldId, first.GetUri("index.html"), 200);
        Assert.Same(second, peer.Requests.Last().LocalContent); Assert.Equal(second.GetUri("index.html"), browser.Source);
        int received = 0; browser.WebMessageReceived += (_, e) => { ++received; Assert.False(e.IsOriginAuthenticated); };
        peer.Client.WebMessageReceived(oldId, "old", first.Origin); Assert.Equal(0, received);
        peer.Client.WebMessageReceived(peer.Id, "current", second.Origin); Assert.Equal(1, received);
        Assert.True(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.ConfigurableLocalContent));
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging));
    }

    [Fact]
    public void RecreatedUnsupportedPeerDoesNotReplayPreviouslySupportedPolicy()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var provider = new Provider();
        var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var old = provider.Peers.Single(); old.Ready(); browser.NavigateToLocalContent(Content(WebViewLocalContentPolicy.Application));
        old.Client.NavigationCompleted(old.Id, browser.Source, 200); using var transport = new WebViewTransport(browser); var document = transport.CurrentDocument;
        provider.Supported = false; window.NativeViews.SetAvailable(false); window.NativeViews.SetAvailable(true);
        Assert.True(document.CancellationToken.IsCancellationRequested); var current = provider.Peers.Last(); current.Ready();
        Assert.Empty(current.Requests); Assert.False(browser.Reload()); old.Client.NavigationCompleted(old.Id, null, 200);
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.ConfigurableLocalContent));
    }

    [Fact]
    public void ViewInstancesRetainIndependentPolicyAndCancellationState()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var one = new Provider(); var two = new Provider();
        var first = form.Controls.Add(new WebView(one) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        var second = form.Controls.Add(new WebView(two) { Bounds = new(110, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var a = one.Peers.Single(); var b = two.Peers.Single(); a.Ready(); b.Ready();
        var application = Content(WebViewLocalContentPolicy.Application); var strict = Content(WebViewLocalContentPolicy.Default);
        first.NavigateToLocalContent(application); second.NavigateToLocalContent(strict);
        a.Client.NavigationCompleted(a.Id, first.Source, 200); b.Client.NavigationCompleted(b.Id, second.Source, 200);
        using var transport = new WebViewTransport(second); var retained = transport.CurrentDocument; first.Stop();
        Assert.False(retained.CancellationToken.IsCancellationRequested);
        Assert.Same(application.Policy, a.Requests.Last().LocalContent!.Policy); Assert.Same(strict.Policy, b.Requests.Last().LocalContent!.Policy);
        Assert.True(second.Reload()); Assert.Same(strict, b.Requests.Last().LocalContent);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SelfConnectionsDoNotPermitForeignTopLevelNavigation(bool otherPort)
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var provider = new Provider();
        var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100) });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        var content = Content(WebViewLocalContentPolicy.Application); browser.NavigateToLocalContent(content); peer.Client.NavigationCompleted(peer.Id, browser.Source, 200);
        var target = otherPort ? new UriBuilder(content.Origin) { Port = 444 }.Uri : new Uri("https://foreign.invalid/");
        Assert.False(peer.Client.NavigationStarting(peer.Client.BeginNativeNavigation(), target, true));
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging));
    }
}
