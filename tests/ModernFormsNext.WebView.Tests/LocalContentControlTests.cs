using ModernFormsNext.Testing;

namespace ModernFormsNext.WebView.Tests;

public sealed class LocalContentControlTests
{
    private static WebViewLocalContent Content() => new(new Dictionary<string, byte[]> { ["index.html"] = [] });

    [Fact]
    public void SourceAndCurrentRequestIdDoNotAuthenticateBaselineStrings()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.SupportsLocalContent = true; peer.Ready();
        browser.NavigateToLocalContent(Content());
        Uri? reported = null; browser.WebMessageReceived += (_, e) => reported = e.Source;
        var forged = new Uri("https://attacker.test/");
        peer.Client.WebMessageReceived(peer.Id, "untrusted", forged);
        Assert.Equal(forged, reported); // Compatibility event carries data, never an authorization decision.
    }

    [Fact]
    public void PendingContentReplaysAndUnsupportedPeersFailClosed()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100) });
        var content = Content(); browser.NavigateToLocalContent(content);
        using var window = app.Show(form); var peer = provider.Peers.Single();
        WebViewErrorCategory? error = null; browser.NavigationFailed += (_, e) => error = e.Category;
        peer.Ready(); Assert.Equal(WebViewErrorCategory.Engine, error); Assert.Empty(peer.Requests);
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.LocalContent));
        Assert.False(browser.Reload()); Assert.Empty(peer.Requests);
        Assert.Throws<NotSupportedException>(() => browser.NavigateToLocalContent(content));
        browser.Navigate(new Uri("https://example.com/")); Assert.Single(peer.Requests);
    }

    [Fact]
    public void ReloadNativeNavigationCancellationAndRetirementRetainOnlyCurrentCollection()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.SupportsLocalContent = true; peer.Ready();
        var content = Content(); browser.NavigateToLocalContent(content);
        Assert.True(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.LocalContent));
        Assert.Same(content, peer.Requests.Last().LocalContent);
        peer.Client.NavigationCompleted(peer.Id, content.GetUri("index.html"), 200);
        Assert.True(browser.Reload());
        peer.Client.NavigationCompleted(peer.Id, content.GetUri("index.html"), 200);
        long nativeId = peer.Client.BeginNativeNavigation();
        Assert.False(peer.Client.NavigationStarting(nativeId, new Uri("https://foreign.test/"), true));
        Assert.False(browser.IsLoading);
        long relativeId = peer.Client.BeginNativeNavigation();
        Assert.False(peer.Client.NavigationStarting(relativeId, new Uri("relative.html", UriKind.Relative), false));
        int messages = 0; browser.WebMessageReceived += (_, _) => ++messages;
        browser.Stop(); peer.Client.WebMessageReceived(nativeId, "stale", content.Origin); Assert.Equal(0, messages);
        browser.NavigateToLocalContent(content);
        browser.Dispose(); peer.Client.WebMessageReceived(peer.Id, "retired", content.Origin); Assert.Equal(0, messages);
        Assert.Throws<ObjectDisposedException>(() => browser.NavigateToLocalContent(content));
    }

    [Fact]
    public void CancelAndReentrantReplacementDoNotPromoteStaleContentOrMessages()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.SupportsLocalContent = true; peer.Ready();
        var first = Content(); var second = Content(); browser.NavigateToLocalContent(first); long oldId = peer.Id;
        peer.Client.NavigationCompleted(oldId, first.GetUri("index.html"), 200);
        browser.NavigationStarting += (_, e) => { if (e.Uri?.Host == second.Origin.Host) { e.Cancel = true; browser.NavigateToHtml("replacement"); } };
        browser.NavigateToLocalContent(second);
        peer.Client.NavigationCompleted(oldId, first.GetUri("index.html"), 200);
        Assert.Null(browser.Source); Assert.Equal("replacement", peer.Requests.Last().Html); Assert.Null(peer.Requests.Last().LocalContent);
        int messages = 0; browser.WebMessageReceived += (_, _) => ++messages;
        peer.Client.WebMessageReceived(oldId, "forged source", second.Origin); Assert.Equal(0, messages);
    }
}
