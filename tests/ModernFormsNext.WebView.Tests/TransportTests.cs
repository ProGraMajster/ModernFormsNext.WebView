using ModernFormsNext.Testing;

namespace ModernFormsNext.WebView.Tests;

public sealed class TransportTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly ModernFormsTestHost App = ModernFormsTestHost.Create();
        internal readonly Form Form = new();
        internal readonly FakeProvider Provider = new();
        internal readonly WebView Browser;
        internal readonly TestWindowHost Window;
        internal FakePeer Peer => Provider.Peers.Last();
        internal Fixture()
        {
            Browser = Form.Controls.Add(new WebView(Provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
            Window = App.Show(Form); Peer.Ready();
        }
        internal void Load() { Browser.NavigateToHtml("document"); Peer.Client.NavigationCompleted(Peer.Id, null, 200); }
        public void Dispose() { Window.Dispose(); Form.Dispose(); App.Dispose(); }
    }

    [Fact]
    public void ReadinessAndTokensComeFromCanonicalControl()
    {
        using var f = new Fixture(); using var transport = new WebViewTransport(f.Browser);
        Assert.False(transport.CurrentDocument.IsReady);
        f.Load(); var first = transport.CurrentDocument; Assert.True(first.IsReady);
        Assert.Equal(f.Peer.Id, first.RequestId); Assert.Equal(f.Browser.GetDiagnostics().PeerGeneration, first.PeerGeneration);
        f.Browser.NavigateToHtml("new"); Assert.True(first.CancellationToken.IsCancellationRequested);
        Assert.False(transport.CurrentDocument.IsReady); Assert.NotEqual(first.RequestId, transport.CurrentDocument.RequestId);
        f.Peer.Client.NavigationCompleted(f.Peer.Id, null, 200); Assert.True(transport.CurrentDocument.IsReady);
        f.Browser.Stop(); Assert.False(transport.CurrentDocument.IsReady);
    }

    [Fact]
    public async Task DisposingOneTransportCancelsOnlyItsOperationsAndBorrowsBrowser()
    {
        Task<string> canceled, retained, result; Task posted;
        using (var f = new Fixture())
        {
            f.Load(); using var first = new WebViewTransport(f.Browser); using var second = new WebViewTransport(f.Browser);
            var document = first.CurrentDocument;
            canceled = first.ExecuteScriptAsync("value"); retained = second.ExecuteScriptAsync("value");
            first.Dispose();
            Assert.True(first.LifetimeToken.IsCancellationRequested); Assert.False(document.CancellationToken.IsCancellationRequested);
            Assert.False(second.LifetimeToken.IsCancellationRequested); Assert.False(retained.IsCompleted); Assert.Equal(0, f.Peer.Disposals);
            second.Dispose();
            f.Peer.Script.SetResult("{\"answer\":42}");
            using var third = new WebViewTransport(f.Browser);
            result = third.ExecuteScriptAsync("value"); posted = third.PostMessageAsync("quoted \" \\ 😀");
            Assert.Equal("quoted \" \\ 😀", f.Peer.Message);
            Assert.Throws<ObjectDisposedException>(() => { _ = first.ExecuteScriptAsync("value"); });
            Assert.Throws<ObjectDisposedException>(() => first.CurrentDocument);
        }
        // xUnit continuation is not the headless UI thread. Finish all UI work and host
        // cleanup before awaiting detached outcomes, following the existing test contract.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retained);
        Assert.Equal("{\"answer\":42}", await result); await posted;
    }

    [Fact]
    public async Task CanonicalRetirementCancelsPendingWorkAndDoesNotDisposeTransport()
    {
        Task<string> pending;
        using (var f = new Fixture())
        {
            f.Load(); using var transport = new WebViewTransport(f.Browser);
            var old = transport.CurrentDocument; var retired = f.Peer; int messages = 0;
            transport.MessageReceived += (_, _) => ++messages; pending = transport.ExecuteScriptAsync("value");
            var factory = f.Browser.PeerFactory; f.Browser.PeerFactory = null;
            Assert.True(old.CancellationToken.IsCancellationRequested); Assert.False(transport.LifetimeToken.IsCancellationRequested);
            f.Browser.PeerFactory = factory; f.Peer.Ready(); f.Peer.Client.NavigationCompleted(f.Peer.Id, null, 200);
            Assert.True(transport.CurrentDocument.IsReady); Assert.NotEqual(old.PeerGeneration, transport.CurrentDocument.PeerGeneration);
            retired.Client.WebMessageReceived(old.RequestId, "old", null); Assert.Equal(0, messages);
            f.Peer.Client.WebMessageReceived(f.Peer.Id, "new", new Uri("https://foreign.test/")); Assert.Equal(1, messages);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void BaselineReentrancyCannotRelabelMessageAsReplacementDocument()
    {
        using var f = new Fixture(); f.Load(); long old = f.Peer.Id;
        f.Browser.WebMessageReceived += (_, _) => f.Browser.NavigateToHtml("replacement");
        using var transport = new WebViewTransport(f.Browser); int forwarded = 0;
        transport.MessageReceived += (_, _) => ++forwarded;
        f.Peer.Client.WebMessageReceived(old, "old", null); Assert.Equal(0, forwarded);
    }

    [Fact]
    public void ForwardedSnapshotKeepsOriginalIdentityDuringObserverNavigation()
    {
        using var f = new Fixture(); f.Load(); using var transport = new WebViewTransport(f.Browser); long old = f.Peer.Id;
        transport.MessageReceived += (_, _) => f.Browser.NavigateToHtml("replacement");
        WebViewTransportMessageEventArgs? captured = null; transport.MessageReceived += (_, e) => captured = e;
        f.Peer.Client.WebMessageReceived(old, "untrusted", new Uri("https://claimed.test/"));
        Assert.NotNull(captured); Assert.Equal(old, captured.Document.RequestId);
        Assert.True(captured.Document.CancellationToken.IsCancellationRequested); Assert.False(captured.IsOriginAuthenticated);
        Assert.Equal("https://claimed.test/", captured.Source?.AbsoluteUri);
    }

    [Fact]
    public void DisposalUnsubscribesAndKeepsBaselineAvailable()
    {
        using var f = new Fixture(); f.Load(); var transport = new WebViewTransport(f.Browser);
        int forwarded = 0, baseline = 0; transport.MessageReceived += (_, _) => ++forwarded;
        f.Browser.WebMessageReceived += (_, _) => ++baseline;
        transport.Dispose(); transport.Dispose(); f.Peer.Client.WebMessageReceived(f.Peer.Id, "baseline", null);
        Assert.Equal(0, forwarded); Assert.Equal(1, baseline); Assert.True(f.Browser.IsInitialized);
    }

    [Fact]
    public async Task CallerCancellationAndOwnerDisposalSettleWithoutNativeCallback()
    {
        Task<string> pending, ownerPending;
        using (var f = new Fixture())
        {
            f.Load(); using var transport = new WebViewTransport(f.Browser);
            using var caller = new CancellationTokenSource(); pending = transport.ExecuteScriptAsync("value", caller.Token);
            caller.Cancel(); ownerPending = transport.ExecuteScriptAsync("value"); f.Browser.Dispose();
            Assert.False(f.Peer.Script.Task.IsCompleted);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ownerPending);
    }
}
