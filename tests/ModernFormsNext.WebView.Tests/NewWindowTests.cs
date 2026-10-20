using System.Text.Json;
using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WebView.Tests;

public sealed class NewWindowTests
{
    private sealed class PopupPeer(INativeViewSite site, IWebViewPeerClient client, object environment) : FakePeer(site, client), IWebViewNewWindowPeer, IWebViewNavigationFilterPeer
    {
        private object Environment { get; } = environment;
        internal bool Used, Supported = true;
        internal int PolicyChanges;
        internal WebViewNavigationRequest? Prepared;
        public bool SupportsNewWindows => Supported && Site.IsCurrent;
        public bool SupportsNavigationSchemeFiltering => Site.IsCurrent;
        public void ApplyNewWindowPolicy(WebViewNewWindowPolicy policy)
        { ++PolicyChanges; Settings = Settings! with { NewWindowPolicy = policy }; }
        public bool CanAcceptNewWindowFrom(IWebViewNewWindowPeer source) => !Used && Site.IsCurrent &&
            source is PopupPeer other && !ReferenceEquals(this, other) && ReferenceEquals(Environment, other.Environment);
        public void PrepareNewWindow(WebViewNavigationRequest request) { Used = true; Prepared = request; Id = request.Id; }
        public void CancelNewWindow(long id) { if (Prepared?.Id == id) Prepared = null; }
        internal IWebViewNewWindowPeerClient PopupClient => (IWebViewNewWindowPeerClient)Client;
        internal WebViewNewWindowTarget? Ask(bool user = false, long? id = null, Uri? uri = null)
            => PopupClient.NewWindowRequested(id ?? Id, uri, user);
    }
    private sealed class Provider : IWebViewPeerFactoryProvider
    {
        private readonly object environment = new();
        internal readonly List<PopupPeer> Peers = [];
        public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
        { var peer = new PopupPeer(site, client, environment); Peers.Add(peer); return peer; }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly ModernFormsTestHost App = ModernFormsTestHost.Create();
        internal readonly Form Form = new();
        internal readonly Provider Provider = new();
        internal readonly WebView Source;
        internal readonly TestWindowHost Window;
        internal PopupPeer Peer => Provider.Peers[0];
        internal Fixture()
        {
            Source = Form.Controls.Add(new WebView(Provider) { Bounds = new(0, 0, 140, 100), IsJavaScriptEnabled = true });
            Window = App.Show(Form); Peer.Ready();
            Source.NavigateToHtml("<p>source</p>"); Peer.Client.NavigationCompleted(Peer.Id, null, 200);
        }
        internal (WebView View, PopupPeer Peer) Target()
        {
            var view = Form.Controls.Add(new WebView(Provider) { Bounds = new(0, 110, 140, 60), IsJavaScriptEnabled = true });
            var peer = Provider.Peers.Last(); peer.Ready(); return (view, peer);
        }
        public void Dispose() { Form.Dispose(); App.Dispose(); }
    }

    [Fact]
    public void DenyIsCompatibleAndDoesNotOfferUntrustedIntents()
    {
        using var f = new Fixture(); int calls = 0;
        f.Source.NewWindowRequested += (_, _) => ++calls;
        Assert.Null(f.Peer.Ask()); Assert.Null(f.Peer.Ask(true)); Assert.Equal(0, calls);
        Assert.Equal(WebViewNewWindowPolicy.Deny, f.Peer.Settings!.NewWindowPolicy);
        Assert.True(f.Source.BrowserCapabilities.HasFlag(WebViewCapabilities.NewWindows));
        Assert.False(f.Source.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging));
        var legacy = new WebViewSettings(true, "agent"); var (js, agent) = legacy;
        Assert.True(js); Assert.Equal("agent", agent); Assert.Equal(WebViewNewWindowPolicy.Deny, legacy.NewWindowPolicy);
    }
    [Fact]
    public void NativeGestureIsOnlyAnIntentFilterAndNoSelectionStillDenies()
    {
        using var f = new Fixture(); f.Source.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
        int calls = 0; f.Source.NewWindowRequested += (_, e) => { ++calls; Assert.True(e.IsUserInitiated); Assert.Null(e.RequestedUri); };
        Assert.Null(f.Peer.Ask()); Assert.Null(f.Peer.Ask(true)); Assert.Equal(1, calls);
    }
    [Fact]
    public void DynamicRiskSelectionHasRedactedWarningAndNoDocumentOrLayoutReplacement()
    {
        using var f = new Fixture(); using var transport = new WebViewTransport(f.Source);
        var document = transport.CurrentDocument; var warnings = new List<WebViewSecurityWarningEventArgs>();
        f.Source.SecurityWarning += (_, e) => warnings.Add(e);
        f.Source.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated; Assert.Empty(warnings);
        var dynamicValue = (WebViewNewWindowPolicy)JsonSerializer.Deserialize<int>("2");
        f.Source.NewWindowPolicy = dynamicValue;
        var warning = Assert.Single(warnings); Assert.Equal("MFNWVSEC001", warning.DiagnosticId);
        Assert.Contains("phishing", warning.Message); Assert.Contains("UserInitiated", warning.Message);
        Assert.DoesNotContain("source", warning.Message); Assert.DoesNotContain("http", warning.Message);
        Assert.Equal(document, transport.CurrentDocument); Assert.False(document.CancellationToken.IsCancellationRequested);
        Assert.Equal(2, f.Peer.PolicyChanges); Assert.Equal(0, f.Peer.Stops);
    }
    [Theory]
    [InlineData(-1)] [InlineData(3)] [InlineData(int.MaxValue)]
    public void InvalidPolicyCannotMutateNativeOrWarn(int value)
    {
        using var f = new Fixture(); int warnings = 0; f.Source.SecurityWarning += (_, _) => ++warnings;
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)value);
        Assert.Equal(WebViewNewWindowPolicy.Deny, f.Source.NewWindowPolicy); Assert.Equal(0, warnings); Assert.Equal(0, f.Peer.PolicyChanges);
    }
    [Fact]
    public void LegacyAdaptersRejectReadyOptInAndQueuedOptInFailsBeforeReplay()
    {
        using var app = ModernFormsTestHost.Create(); using var form = new Form(); var provider = new FakeProvider();
        var ready = form.Controls.Add(new WebView(provider) { Bounds = new(0, 0, 90, 90) }); using var window = app.Show(form);
        provider.Peers[0].Ready();
        Assert.Throws<NotSupportedException>(() => ready.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated);
        Assert.Equal(WebViewNewWindowPolicy.Deny, ready.NewWindowPolicy);
        var queued = form.Controls.Add(new WebView(provider) { Bounds = new(0, 100, 90, 90), NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated });
        queued.Navigate(new("https://queued.invalid/")); WebViewErrorCategory? failure = null;
        queued.InitializationFailed += (_, e) => failure = e.Category; provider.Peers[1].Ready();
        Assert.Equal(WebViewErrorCategory.Engine, failure); Assert.Empty(provider.Peers[1].Requests); Assert.False(queued.IsInitialized);
    }
    [Fact]
    public void TargetUsesCanonicalRequestAndCanCloseOnlyThroughAppDecision()
    {
        using var f = new Fixture(); var target = f.Target(); f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) => e.OpenIn(target.View);
        using var transport = new WebViewTransport(target.View); var old = transport.CurrentDocument;
        var handoff = Assert.IsType<WebViewNewWindowTarget>(f.Peer.Ask(uri: new("https://popup.test/")));
        Assert.True(old.CancellationToken.IsCancellationRequested); Assert.True(handoff.IsCurrent);
        Assert.Equal(transport.CurrentDocument.CancellationToken, handoff.DocumentCancellationToken);
        Assert.Equal(transport.CurrentDocument.RequestId, handoff.Request.Id); Assert.True(target.View.IsLoading);
        target.Peer.PrepareNewWindow(handoff.Request);
        Assert.True(target.Peer.Client.NavigationStarting(handoff.Request.Id, handoff.Request.Uri, false));
        target.Peer.Client.NavigationCompleted(handoff.Request.Id, handoff.Request.Uri, 200);
        Assert.True(transport.CurrentDocument.IsReady); Assert.Equal(handoff.Request.Uri, target.View.Source);
        int closes = 0; target.View.CloseRequested += (_, _) => ++closes;
        target.Peer.PopupClient.CloseRequested(handoff.Request.Id); Assert.Equal(1, closes); Assert.True(target.View.IsInitialized);
        target.Peer.PopupClient.CloseRequested(handoff.Request.Id - 1); Assert.Equal(1, closes);
        Assert.Throws<InvalidOperationException>(() => f.Peer.Ask());
    }
    [Theory]
    [InlineData("stop")] [InlineData("navigate")] [InlineData("disable-js")] [InlineData("deny")] [InlineData("policy-round-trip")] [InlineData("dispose")]
    public void ReentrantSourceChangesDenyHandoffWithoutConsumingTarget(string operation)
    {
        using var f = new Fixture(); var target = f.Target(); f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) =>
        {
            e.OpenIn(target.View);
            switch (operation)
            {
                case "stop": f.Source.Stop(); break;
                case "navigate": f.Source.NavigateToHtml("replacement"); break;
                case "disable-js": f.Source.IsJavaScriptEnabled = false; break;
                case "deny": f.Source.NewWindowPolicy = WebViewNewWindowPolicy.Deny; break;
                case "policy-round-trip": f.Source.NewWindowPolicy = WebViewNewWindowPolicy.Deny; f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2; break;
                default: f.Source.Dispose(); break;
            }
        };
        Assert.Null(f.Peer.Ask()); Assert.Null(target.Peer.Prepared); Assert.False(target.Peer.Used);
    }
    [Fact]
    public void TargetNavigationAndRetirementAfterSelectionCannotBeOverwritten()
    {
        using var f = new Fixture(); var target = f.Target(); f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) => { e.OpenIn(target.View); target.View.NavigateToHtml("newer target"); };
        Assert.Null(f.Peer.Ask()); Assert.Single(target.Peer.Requests); Assert.Equal("newer target", target.Peer.Requests[0].Html);
        Assert.Null(target.Peer.Prepared); Assert.Equal(0, target.Peer.Stops);
    }
    [Fact]
    public void IncompatibleSelfUnreadyAndRetainedDecisionsAreExplicitlyRejected()
    {
        using var f = new Fixture(); var target = f.Target(); f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        WebViewNewWindowRequestedEventArgs? saved = null;
        f.Source.NewWindowRequested += (_, e) =>
        {
            saved = e; Assert.Throws<InvalidOperationException>(() => e.OpenIn(f.Source));
            using var unready = new WebView(); Assert.Throws<InvalidOperationException>(() => e.OpenIn(unready));
        };
        Assert.Null(f.Peer.Ask()); Assert.NotNull(saved);
        Assert.Throws<InvalidOperationException>(() => saved.OpenIn(target.View));
        Assert.Null(f.Peer.Ask(id: f.Peer.Id + 1));
    }

    [Fact]
    public void DifferentNativeEnvironmentAndUsedScriptTargetCannotBeSelected()
    {
        using var f = new Fixture(); var otherProvider = new Provider();
        var other = f.Form.Controls.Add(new WebView(otherProvider) { Bounds = new(0, 200, 80, 60) }); otherProvider.Peers[0].Ready();
        var target = f.Target(); target.Peer.Used = true;
        f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) =>
        {
            Assert.Throws<InvalidOperationException>(() => e.OpenIn(other));
            Assert.Throws<InvalidOperationException>(() => e.OpenIn(target.View));
        };
        Assert.Null(f.Peer.Ask());
    }

    [Fact]
    public void DisposedTargetAndRejectedReservationCannotReplaceANewerRequest()
    {
        using var f = new Fixture(); var target = f.Target(); f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) => e.OpenIn(target.View);
        var handoff = Assert.IsType<WebViewNewWindowTarget>(f.Peer.Ask());
        target.Peer.PrepareNewWindow(handoff.Request);
        target.View.NavigateToHtml("newer"); handoff.Reject();
        Assert.False(handoff.IsCurrent); Assert.True(target.View.IsLoading);
        Assert.Equal("newer", target.Peer.Requests.Last().Html);
        target.View.Dispose(); Assert.False(handoff.IsCurrent);
    }

    [Fact]
    public void TargetCancellationObserverMayRetireSourceBeforeNativePreparation()
    {
        using var f = new Fixture(); var target = f.Target();
        using var transport = new WebViewTransport(target.View);
        using var registration = transport.CurrentDocument.CancellationToken.Register(f.Source.Stop);
        f.Source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        f.Source.NewWindowRequested += (_, e) => e.OpenIn(target.View);
        int failures = 0; target.View.NavigationFailed += (_, e) => { Assert.Equal(WebViewErrorCategory.Engine, e.Category); ++failures; };
        Assert.Null(f.Peer.Ask()); Assert.Null(target.Peer.Prepared);
        Assert.Equal(1, failures); Assert.False(target.View.IsLoading);
    }

    [Fact]
    public void FreshTargetMayConfigureItsOwnSchemePolicyWithoutBeingMistakenForANavigation()
    {
        using var f = new Fixture(); var target = f.Target();
        target.View.AllowedNavigationSchemes = WebViewNavigationSchemes.Https;
        using var transport = new WebViewTransport(target.View);
        long before = transport.CurrentDocument.RequestId;
        Assert.True(before > 0); // The existing scheme setter revokes through canonical Stop.
        f.Source.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
        f.Source.NewWindowRequested += (_, e) => e.OpenIn(target.View);
        var handoff = Assert.IsType<WebViewNewWindowTarget>(f.Peer.Ask(true, uri: new("https://target.test/")));
        Assert.True(handoff.Request.Id > before);
        target.Peer.PrepareNewWindow(handoff.Request);
        Assert.True(target.Peer.Client.NavigationStarting(handoff.Request.Id, handoff.Request.Uri, false));
        Assert.False(target.Peer.Client.NavigationStarting(handoff.Request.Id, new("http://denied.test/"), true));
    }
}
