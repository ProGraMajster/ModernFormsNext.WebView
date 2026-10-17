using ModernFormsNext.Testing;

namespace ModernFormsNext.WebView.Tests;

public sealed class MessageSecurityTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://attacker.test/")]
    [InlineData("https://application.test/")]
    [InlineData("data:text/html,opaque")]
    [InlineData("about:blank")]
    public void SourceAndSpoofableCurrentIdentityNeverGrantAuthentication(string? address)
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        browser.Navigate(new Uri("https://application.test/"));
        WebViewMessageReceivedEventArgs? message = null; browser.WebMessageReceived += (_, e) => message = e;
        peer.Client.WebMessageReceived(peer.Id, "untrusted", address is null ? null : new Uri(address));
        Assert.NotNull(message); Assert.False(message.IsOriginAuthenticated);
        Assert.Equal(peer.Id, message.RequestId); Assert.Equal(browser.GetDiagnostics().PeerGeneration, message.PeerGeneration);
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging));
    }

    [Theory]
    [InlineData("navigate")]
    [InlineData("stop")]
    [InlineData("cancel")]
    [InlineData("disable")]
    [InlineData("dispose")]
    public void ObsoleteDocumentCannotDeliverAfterLifetimeBoundary(string boundary)
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        browser.NavigateToHtml("old"); long old = peer.Id; int received = 0;
        browser.WebMessageReceived += (_, _) => ++received;
        switch (boundary)
        {
            case "navigate": browser.NavigateToHtml("replacement"); break;
            case "stop": browser.Stop(); break;
            case "cancel": browser.NavigationStarting += (_, e) => e.Cancel = true; browser.Navigate(new Uri("https://foreign.test/")); break;
            case "disable": browser.IsJavaScriptEnabled = false; break;
            case "dispose": browser.Dispose(); break;
        }
        peer.Client.WebMessageReceived(old, "late", new Uri("https://application.test/"));
        Assert.Equal(0, received);
    }

    [Fact]
    public void ReentrantObserverDoesNotRelabelTheOriginalEvent()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider(); var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        browser.NavigateToHtml("old"); long old = peer.Id;
        browser.WebMessageReceived += (_, _) => browser.NavigateToHtml("new");
        WebViewMessageReceivedEventArgs? original = null; browser.WebMessageReceived += (_, e) => original = e;
        peer.Client.WebMessageReceived(old, "event", null);
        Assert.NotNull(original); Assert.Equal(old, original.RequestId); Assert.NotEqual(peer.Id, original.RequestId);
        Assert.False(original.IsOriginAuthenticated);
    }
}
