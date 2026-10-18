using System.ComponentModel;
using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WebView.Tests;

public sealed class NavigationSchemeTests
{
    private sealed class FilterPeer(INativeViewSite site, IWebViewPeerClient client) : FakePeer(site, client), IWebViewNavigationFilterPeer
    { public bool SupportsNavigationSchemeFiltering { get; set; } = true; }
    private sealed class Provider : IWebViewPeerFactoryProvider
    {
        internal bool Supported = true;
        internal readonly List<FilterPeer> Peers = [];
        public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
        {
            var peer = new FilterPeer(site, client) { SupportsLocalContent = true, SupportsNavigationSchemeFiltering = Supported };
            Peers.Add(peer); return peer;
        }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly ModernFormsTestHost App = ModernFormsTestHost.Create();
        internal readonly Form Form = new();
        internal readonly Provider Provider = new();
        internal readonly WebView Browser;
        internal readonly TestWindowHost Window;
        internal FilterPeer Peer => Provider.Peers.Last();
        internal Fixture()
        {
            Browser = Form.Controls.Add(new WebView(Provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
            Window = App.Show(Form);
        }
        internal void Complete(Uri? uri = null)
        { Peer.Client.NavigationCompleted(Peer.Id, uri ?? Browser.Source, 200); }
        public void Dispose() { Form.Dispose(); App.Dispose(); }
    }

    [Theory]
    [InlineData(0, "http://test.invalid/", false)] [InlineData(0, "https://test.invalid/", false)] [InlineData(0, null, false)]
    [InlineData(1, "http://test.invalid/", true)] [InlineData(1, "https://test.invalid/", false)] [InlineData(1, null, false)]
    [InlineData(2, "http://test.invalid/", false)] [InlineData(2, "https://test.invalid/", true)] [InlineData(2, null, false)]
    [InlineData(3, "http://test.invalid/", true)] [InlineData(3, "https://test.invalid/", true)] [InlineData(3, null, false)]
    [InlineData(4, "http://test.invalid/", false)] [InlineData(4, "https://test.invalid/", false)] [InlineData(4, null, true)]
    [InlineData(5, "http://test.invalid/", true)] [InlineData(5, "https://test.invalid/", false)] [InlineData(5, null, true)]
    [InlineData(6, "http://test.invalid/", false)] [InlineData(6, "https://test.invalid/", true)] [InlineData(6, null, true)]
    [InlineData(7, "http://test.invalid/", true)] [InlineData(7, "https://test.invalid/", true)] [InlineData(7, null, true)]
    public void EachSchemeCombinationGatesCommandsBeforeNativeSubmission(int flags, string? address, bool allowed)
    {
        using var f = new Fixture(); f.Peer.Ready();
        f.Browser.AllowedNavigationSchemes = (WebViewNavigationSchemes)flags;
        using var transport = new WebViewTransport(f.Browser); var before = transport.CurrentDocument;
        Uri? uri = address is null ? null : new(address);
        if (allowed) { f.Browser.Navigate(uri); Assert.Single(f.Peer.Requests); }
        else
        {
            Assert.Throws<ArgumentException>(() => f.Browser.Navigate(uri));
            Assert.Empty(f.Peer.Requests);
            Assert.Equal(before.RequestId, transport.CurrentDocument.RequestId);
            Assert.False(before.CancellationToken.IsCancellationRequested);
        }
    }

    [Theory]
    [InlineData(-1)] [InlineData(8)] [InlineData(1024)] [InlineData(int.MaxValue)]
    public void InvalidBitsDoNotChangeSettingsOrDocument(int invalid)
    {
        using var f = new Fixture(); f.Peer.Ready();
        using var transport = new WebViewTransport(f.Browser); var before = transport.CurrentDocument;
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Browser.AllowedNavigationSchemes = (WebViewNavigationSchemes)invalid);
        Assert.Equal(WebViewNavigationSchemes.Default, f.Browser.AllowedNavigationSchemes);
        Assert.Equal(before, transport.CurrentDocument); Assert.Equal(0, f.Peer.Stops);
    }

    [Fact]
    public void DefaultIsCompatibleAndLegacyPeerExplicitlyRejectsOptIn()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form();
        var provider = new FakeProvider();
        var browser = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 100, 100) });
        using var window = app.Show(form); var peer = provider.Peers.Single(); peer.Ready();
        Assert.Equal((WebViewNavigationSchemes)7, browser.AllowedNavigationSchemes);
        Assert.False(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering));
        browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Default;
        browser.Navigate(new("http://legacy.test/")); Assert.Single(peer.Requests);
        Assert.Throws<NotSupportedException>(() => browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https);
        Assert.Equal(WebViewNavigationSchemes.Default, browser.AllowedNavigationSchemes);
        Assert.Equal(0, peer.Stops);
        Assert.False(TypeDescriptor.GetProperties(browser)[nameof(WebView.AllowedNavigationSchemes)]!.IsBrowsable);
    }

    [Fact]
    public void QueuedUnsupportedPolicyCannotLoadOrReloadButDefaultRecoveryWorks()
    {
        using var f = new Fixture(); f.Peer.SupportsNavigationSchemeFiltering = false;
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        f.Browser.Navigate(new("https://queued.test/"));
        WebViewErrorCategory? failure = null; f.Browser.NavigationFailed += (_, e) => failure = e.Category;
        f.Peer.Ready();
        Assert.Equal(WebViewErrorCategory.Engine, failure); Assert.Empty(f.Peer.Requests);
        Assert.False(f.Browser.Reload()); Assert.False(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering));
        Assert.Throws<NotSupportedException>(() => f.Browser.Navigate(new("https://ready.test/")));
        Assert.Throws<NotSupportedException>(() => f.Browser.NavigateToHtml("raw"));
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Default;
        f.Browser.Navigate(new("https://recovered.test/")); Assert.Single(f.Peer.Requests);
    }

    [Fact]
    public void EqualPolicyIsNoOpAndInstancesDoNotShareState()
    {
        using var f = new Fixture(); f.Peer.Ready();
        using var second = new WebView(f.Provider);
        f.Browser.Navigate(new("https://current.test/")); f.Complete();
        using var transport = new WebViewTransport(f.Browser); var before = transport.CurrentDocument;
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Default;
        Assert.Equal(before, transport.CurrentDocument); Assert.Equal(0, f.Peer.Stops);
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        Assert.True(before.CancellationToken.IsCancellationRequested); Assert.Equal(1, f.Peer.Stops);
        Assert.Equal(WebViewNavigationSchemes.Default, second.AllowedNavigationSchemes);
        Assert.False(transport.CurrentDocument.IsReady);
        Assert.True(f.Browser.Reload());
    }

    [Fact]
    public void SourceLocalAndReloadRejectDisallowedSchemesButRawHtmlIsSeparate()
    {
        using var f = new Fixture(); f.Peer.Ready();
        f.Browser.Navigate(new("http://current.test/")); f.Complete();
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        int submitted = f.Peer.Requests.Count;
        Assert.Throws<ArgumentException>(() => f.Browser.Source = f.Browser.Source);
        Assert.Throws<ArgumentException>(() => f.Browser.SourceAddress = "http://other.test/");
        Assert.Throws<ArgumentException>(() => f.Browser.Source = null);
        Assert.False(f.Browser.Reload()); Assert.Equal(submitted, f.Peer.Requests.Count);
        // Static baseline validation remains usable independently of an instance's subset.
        WebView.ValidateUri(new("http://baseline.test/"));
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.None;
        var content = new WebViewLocalContent(new Dictionary<string, byte[]> { ["index.html"] = [] });
        Assert.Throws<ArgumentException>(() => f.Browser.NavigateToLocalContent(content));
        f.Browser.NavigateToHtml("<p>raw</p>"); f.Complete();
        Assert.Null(f.Browser.Source); Assert.True(f.Browser.Reload());
        Assert.True(f.Peer.Client.NavigationStarting(f.Peer.Id, new("data:text/html,raw"), false));
        Assert.False(f.Peer.Client.NavigationStarting(f.Peer.Id, new("about:blank"), true));
    }

    [Theory]
    [InlineData("file:///private", false)] [InlineData("javascript:1", false)]
    [InlineData("data:text/html,raw", false)] [InlineData("about:blank#fragment", false)]
    [InlineData("https://allowed.test/", true)]
    public void FilterCannotExpandBaselineUriPolicy(string address, bool allowed)
    {
        using var f = new Fixture(); f.Peer.Ready();
        if (allowed) f.Browser.Navigate(new(address));
        else { Assert.Throws<ArgumentException>(() => f.Browser.Navigate(new(address))); Assert.Empty(f.Peer.Requests); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReportedDenialPrecedesObserversAndCancelsInFlightDocument(bool redirect)
    {
        Task<string> pending;
        using (var f = new Fixture())
        {
            f.Peer.Ready(); f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
            var committed = new Uri("https://committed.test/");
            f.Browser.Navigate(committed); f.Complete(); f.Browser.Navigate(new("https://loading.test/"));
            using var transport = new WebViewTransport(f.Browser); var before = transport.CurrentDocument;
            pending = f.Browser.ExecuteScriptAsync("pending");
            int starting = 0; f.Browser.NavigationStarting += (_, _) => ++starting;
            WebViewErrorCategory? failure = null; f.Browser.NavigationFailed += (_, e) => failure = e.Category;
            long rejectedId = f.Peer.Id;
            Assert.False(f.Peer.Client.NavigationStarting(rejectedId, new("http://forbidden.test/"), redirect));
            Assert.Equal(0, starting); Assert.Equal(WebViewErrorCategory.InvalidAddress, failure);
            Assert.True(before.CancellationToken.IsCancellationRequested); Assert.Equal(committed, f.Browser.Source);
            Assert.False(f.Browser.IsLoading); Assert.False(transport.CurrentDocument.IsReady);
            f.Peer.Client.NavigationCompleted(rejectedId, new("http://stale.test/"), 200);
            Assert.Equal(committed, f.Browser.Source); Assert.True(f.Browser.Reload());
        }
        // Token cancellation is synchronous; the asynchronous operation settles separately.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void QueuedAndRecreatedRequestsAreRevalidatedAndStaleCallbacksCannotResetPolicy()
    {
        using var f = new Fixture();
        Assert.False(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering));
        f.Browser.Navigate(new("http://queued.test/"));
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        f.Peer.Ready(); Assert.Empty(f.Peer.Requests);
        Assert.Equal(WebViewErrorCategory.InvalidAddress, f.Browser.GetDiagnostics().LastFailure);
        f.Browser.Navigate(new("https://good.test/")); f.Complete();
        var old = f.Peer; long oldId = old.Id;
        f.Window.NativeViews.SetAvailable(false);
        f.Provider.Supported = false; f.Window.NativeViews.SetAvailable(true); f.Peer.Ready();
        Assert.Empty(f.Peer.Requests); Assert.False(f.Browser.Reload());
        f.Window.NativeViews.SetAvailable(false); f.Provider.Supported = true; f.Window.NativeViews.SetAvailable(true); f.Peer.Ready();
        Assert.Equal("good.test", Assert.Single(f.Peer.Requests).Uri!.Host);
        Assert.True(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering));
        old.Client.NavigationCompleted(oldId, new("http://stale.test/"), 200);
        Assert.Equal("good.test", f.Browser.Source!.Host);
        Assert.Equal(WebViewNavigationSchemes.Https, f.Browser.AllowedNavigationSchemes);
        f.Browser.Dispose();
        Assert.Throws<ObjectDisposedException>(() => f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Default);
        Assert.False(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering));
    }

    [Theory]
    [InlineData("policy")] [InlineData("stop")] [InlineData("reload")] [InlineData("submit")] [InlineData("native")]
    public void CancellationCallbackReplacementWinsOverOlderCommand(string command)
    {
        using var f = new Fixture(); f.Peer.Ready();
        f.Browser.Navigate(new("https://old.test/")); f.Complete();
        using var transport = new WebViewTransport(f.Browser);
        using var registration = transport.CurrentDocument.CancellationToken.Register(() => f.Browser.Navigate(new("https://replacement.test/")));
        switch (command)
        {
            case "policy": f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https; break;
            case "stop": f.Browser.Stop(); break;
            case "reload": Assert.False(f.Browser.Reload()); break;
            case "submit": f.Browser.Navigate(new("http://older-command.test/")); break;
            case "native": Assert.Equal(0, f.Peer.Client.BeginNativeNavigation()); break;
        }
        Assert.Equal("replacement.test", f.Browser.Source!.Host);
        Assert.Equal("replacement.test", f.Peer.Requests.Last().Uri!.Host);
        Assert.True(f.Browser.IsLoading); Assert.Equal(0, f.Peer.Stops);
    }

    [Fact]
    public void PolicyFailureCancellationReentrancyAndThrowingObserversFailClosed()
    {
        using var f = new Fixture(); f.Peer.Ready();
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        f.Browser.Navigate(new("https://old.test/"));
        using var transport = new WebViewTransport(f.Browser);
        int failures = 0; f.Browser.NavigationFailed += (_, _) => ++failures;
        using (transport.CurrentDocument.CancellationToken.Register(() => f.Browser.Navigate(new("https://replacement.test/"))))
            Assert.False(f.Peer.Client.NavigationStarting(f.Peer.Id, new("http://denied.test/"), true));
        Assert.Equal("replacement.test", f.Browser.Source!.Host); Assert.True(f.Browser.IsLoading);
        Assert.Equal(0, failures);
        using (transport.CurrentDocument.CancellationToken.Register(() => throw new InvalidOperationException("observer")))
            Assert.Throws<AggregateException>(() => f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https | WebViewNavigationSchemes.AboutBlank);
        Assert.Equal(2, f.Peer.Stops); // Initial policy Stop and finally-protected throwing Stop.
        Assert.False(f.Browser.IsLoading);
    }

    [Fact]
    public void LocalCatalogOriginAndUnauthenticatedMessagesRemainConjunctive()
    {
        using var f = new Fixture(); f.Peer.Ready();
        f.Browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        var content = new WebViewLocalContent(new Dictionary<string, byte[]> { ["index.html"] = [] });
        f.Browser.NavigateToLocalContent(content); f.Complete();
        Assert.False(f.Peer.Client.NavigationStarting(f.Peer.Id, new("https://foreign.test/"), true));
        bool? authenticated = null; f.Browser.WebMessageReceived += (_, e) => authenticated = e.IsOriginAuthenticated;
        f.Peer.Client.WebMessageReceived(f.Peer.Id, "untrusted", content.Origin);
        Assert.Equal(false, authenticated);
        Assert.False(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging));
    }
}
