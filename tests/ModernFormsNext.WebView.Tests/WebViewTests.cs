using System.ComponentModel;
using ModernFormsNext.Testing;
using SkiaSharp;

namespace ModernFormsNext.WebView.Tests;

public sealed class WebViewTests
{
    private sealed class PreviewWebView(FakeProvider provider) : WebView(provider)
    {
        internal void PaintPreview(PaintEventArgs args) => base.OnPaint(args);
    }
    private sealed class Fixture : IDisposable
    {
        public readonly ModernFormsTestHost App = ModernFormsTestHost.Create();
        public readonly Form Form = new() { UseSystemDecorations = true };
        public readonly FakeProvider Provider = new();
        public readonly WebView Browser;
        public readonly TestWindowHost Window;
        public FakePeer Peer => Provider.Peers.Last();
        public Fixture()
        {
            Browser = Form.Controls.Add(new WebView(Provider) { Bounds = new(10, 10, 300, 200) });
            Window = App.Show(Form);
        }
        public void Ready() => Peer.Ready();
        public void Dispose() { Form.Dispose(); App.Dispose(); }
    }

    [Fact]
    public void PreInitializationRequestsReplaceAndSameSourceDoesNotRepeat()
    {
        using var f = new Fixture();
        f.Browser.Source = new("https://first.test"); f.Browser.Source = new("https://last.test");
        Assert.Empty(f.Peer.Requests); f.Ready();
        Assert.Single(f.Peer.Requests); Assert.Equal(f.Browser.Source, f.Peer.Requests[0].Uri);
        f.Browser.Source = new("https://last.test"); Assert.Single(f.Peer.Requests);
        f.Browser.Navigate(f.Browser.Source); Assert.Equal(2, f.Peer.Requests.Count);
    }
    [Fact]
    public void EventOrderAndCompletionExceptionSettleLoading()
    {
        using var f = new Fixture(); f.Ready();
        var events = new List<string>();
        f.Browser.NavigationStarting += (_, _) => { Assert.False(f.Browser.IsLoading); events.Add("starting"); };
        f.Browser.NavigationCompleted += (_, _) => { Assert.True(f.Browser.IsLoading); events.Add("completed"); throw new InvalidOperationException("observer"); };
        f.Browser.Navigate(new("https://page.test")); Assert.True(f.Browser.IsLoading);
        Assert.Throws<InvalidOperationException>(() => f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200));
        Assert.False(f.Browser.IsLoading); Assert.Equal(["starting", "completed"], events);
    }
    [Fact]
    public void CancelRestoresCommittedSourceAndRejectsCompletion()
    {
        using var f = new Fixture(); f.Ready();
        var good = new Uri("https://good.test"); f.Browser.Navigate(good);
        f.Peer.Client.NavigationCompleted(f.Peer.Id, good, 200);
        f.Browser.NavigationStarting += (_, e) => e.Cancel = true;
        f.Browser.Navigate(new("https://blocked.test"));
        f.Peer.Client.NavigationCompleted(f.Peer.Id, new("https://blocked.test"), 200);
        Assert.Equal(good, f.Browser.Source); Assert.False(f.Browser.IsLoading);
    }
    [Fact]
    public void StartingReentrancyCannotOverwriteNewerSource()
    {
        using var f = new Fixture(); f.Ready();
        bool once = false;
        f.Browser.NavigationStarting += (_, _) => { if (!once) { once = true; f.Browser.Navigate(new("https://new.test")); } };
        f.Browser.Navigate(new("https://old.test"));
        var old = f.Peer.Requests[0];
        f.Peer.Client.NavigationCompleted(old.Id, old.Uri, 200);
        Assert.Equal("new.test", f.Browser.Source!.Host); Assert.True(f.Browser.IsLoading);
    }
    [Fact]
    public void CompletionReentrancyDoesNotSettleNewerNavigation()
    {
        using var f = new Fixture(); f.Ready();
        f.Browser.NavigationCompleted += (_, _) => f.Browser.Navigate(new("https://next.test"));
        f.Browser.Navigate(new("https://old.test"));
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        Assert.True(f.Browser.IsLoading); Assert.Equal("next.test", f.Browser.Source!.Host);
    }
    [Fact]
    public void StopRejectsAllOldStateCallbacks()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.Navigate(new("https://stop.test"));
        long old = f.Peer.Id; f.Browser.Stop();
        f.Peer.Client.HistoryChanged(old, true, true);
        f.Peer.Client.NavigationCompleted(old, new("https://stale.test"), 200);
        f.Peer.Client.NavigationFailed(old, null, WebViewErrorCategory.Network);
        Assert.False(f.Browser.IsLoading); Assert.False(f.Browser.CanGoBack);
        Assert.Equal("stop.test", f.Browser.Source!.Host); Assert.Null(f.Browser.GetDiagnostics().LastFailure);
    }
    [Fact]
    public void RecreationReplaysHtmlAndRejectsOldGeneration()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.NavigateToHtml("<p>offline</p>");
        var old = f.Peer; f.Window.NativeViews.SetAvailable(false);
        Assert.Equal(1, old.Disposals); Assert.False(f.Browser.IsInitialized);
        f.Window.NativeViews.SetAvailable(true); f.Ready();
        Assert.Equal("<p>offline</p>", f.Peer.Requests.Single().Html);
        old.Client.Initialized("stale", "stale", null);
        old.Client.NavigationCompleted(old.Id, new("https://stale.test"), 200);
        old.Client.HistoryChanged(old.Id, true, true);
        Assert.Null(f.Browser.Source); Assert.False(f.Browser.CanGoBack);
        Assert.Equal("Fake", f.Browser.GetDiagnostics().Platform);
    }
    [Fact]
    public void HistoryCommandsUseActivePeerAndUnavailableCommandsReturnFalse()
    {
        using var f = new Fixture();
        Assert.False(f.Browser.GoBack()); Assert.False(f.Browser.GoForward()); Assert.False(f.Browser.Reload());
        f.Ready(); f.Browser.Navigate(new("https://page.test"));
        f.Peer.Client.HistoryChanged(f.Peer.Id, true, true);
        Assert.False(f.Browser.GoBack()); Assert.False(f.Browser.GoForward()); Assert.False(f.Browser.Reload());
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        Assert.True(f.Browser.GoBack());
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        Assert.True(f.Browser.GoForward());
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        Assert.True(f.Browser.Reload());
        Assert.Equal(1, f.Peer.Reloads);
    }
    [Fact]
    public void RedirectUpdatesSourceWithoutAnotherRequest()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.Navigate(new("https://initial.test"));
        Assert.True(f.Peer.Client.NavigationStarting(f.Peer.Id, new("https://final.test"), true));
        f.Peer.Client.NavigationCompleted(f.Peer.Id, new("https://final.test"), 200);
        Assert.Single(f.Peer.Requests); Assert.Equal("final.test", f.Browser.Source!.Host);
    }
    [Fact]
    public void InitializationNotificationNavigationIsNotReplayedTwice()
    {
        using var f = new Fixture();
        f.Browser.InitializationStateChanged += (_, _) => { if (f.Browser.IsInitialized) f.Browser.Navigate(new("https://ready.test")); };
        f.Ready(); Assert.Single(f.Peer.Requests);
    }
    [Fact]
    public void FailureIsNeutralAndDoesNotRetryAutomatically()
    {
        using var f = new Fixture(); int failures = 0;
        f.Browser.InitializationFailed += (_, _) => ++failures;
        f.Peer.Client.InitializationFailed(WebViewErrorCategory.Engine);
        Assert.Equal(WebViewInitializationState.Failed, f.Browser.InitializationState);
        Assert.Single(f.Provider.Peers); Assert.Equal(1, failures);
    }
    [Fact]
    public void SettingsAreAppliedBeforeAndAfterInitialization()
    {
        using var f = new Fixture(); Assert.False(f.Peer.Settings!.IsJavaScriptEnabled);
        f.Browser.IsJavaScriptEnabled = true; f.Browser.UserAgent = "Test agent"; f.Ready();
        Assert.Equal(new WebViewSettings(true, "Test agent"), f.Peer.Settings);
        f.Browser.UserAgent = null; Assert.Null(f.Peer.Settings!.UserAgent);
    }
    [Theory]
    [InlineData("")]
    [InlineData("line\nnext")]
    [InlineData("line\rnext")]
    [InlineData("tab\there")]
    public void InvalidUserAgentsAreRejected(string value)
    { using var f = new Fixture(); Assert.Throws<ArgumentException>(() => f.Browser.UserAgent = value); }
    [Theory]
    [InlineData("file:///private")]
    [InlineData("data:text/html,test")]
    [InlineData("custom:action")]
    [InlineData("javascript:alert(1)")]
    [InlineData("about:config")]
    public void DisallowedSchemesAreRejected(string value)
    { using var f = new Fixture(); Assert.Throws<ArgumentException>(() => f.Browser.Source = new(value)); }
    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("https://example.com/")]
    [InlineData("about:blank")]
    public void AllowedSchemesAreAccepted(string value)
    { using var f = new Fixture(); f.Browser.Source = new(value); Assert.Equal(value, f.Browser.Source!.AbsoluteUri); }
    [Fact]
    public void ScriptReadinessIsExplicit()
    {
        using var f = new Fixture(); Assert.Throws<InvalidOperationException>(() => { _ = f.Browser.ExecuteScriptAsync("1"); });
        f.Ready(); Assert.Throws<InvalidOperationException>(() => { _ = f.Browser.ExecuteScriptAsync("1"); });
    }
    [Fact]
    public async Task ScriptResultIsJsonAndCancellationDoesNotWaitForNativeCallback()
    {
        Task<string> canceled, result;
        using (var f = new Fixture())
        {
            f.Ready(); f.Browser.IsJavaScriptEnabled = true;
            using var cancel = new CancellationTokenSource();
            canceled = f.Browser.ExecuteScriptAsync("1", cancel.Token); cancel.Cancel();
            Assert.False(f.Peer.Script.Task.IsCompleted);
            f.Peer.Script.SetResult("{\"answer\":42}");
            result = f.Browser.ExecuteScriptAsync("1");
        }
        // xUnit's async continuation is not a UI thread. Dispose the host and perform all
        // control work before awaiting detached task results; do not weaken thread affinity.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.Equal("{\"answer\":42}", await result);
    }
    [Fact]
    public async Task NavigationAndRetirementCancelOutstandingScripts()
    {
        Task<string> navigation, retirement;
        using (var f = new Fixture())
        {
            f.Ready(); f.Browser.IsJavaScriptEnabled = true;
            navigation = f.Browser.ExecuteScriptAsync("1"); f.Browser.Navigate(new("https://new.test"));
            retirement = f.Browser.ExecuteScriptAsync("1"); f.Window.NativeViews.SetAvailable(false);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => navigation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retirement);
    }
    [Fact]
    public async Task DisposeCancelsScriptAndRejectsMutationsAndCallbacks()
    {
        Task<string> task;
        using (var f = new Fixture())
        {
            f.Ready(); f.Browser.IsJavaScriptEnabled = true;
            task = f.Browser.ExecuteScriptAsync("1"); var old = f.Peer; f.Browser.Dispose();
            old.Client.Initialized("stale", "stale", null);
            Assert.Equal(WebViewInitializationState.Disposed, f.Browser.InitializationState);
            Assert.Throws<ObjectDisposedException>(() => f.Browser.Navigate(new("https://page.test")));
            Assert.Equal(1, old.Disposals);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }
    [Fact]
    public async Task MessagesAreOptInAndGenerationGuarded()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.NavigateToHtml("test");
        var messages = new List<string>(); f.Browser.WebMessageReceived += (_, e) => messages.Add(e.Message);
        f.Peer.Client.WebMessageReceived(f.Peer.Id, "disabled", null); Assert.Empty(messages);
        f.Browser.IsJavaScriptEnabled = true;
        f.Peer.Client.WebMessageReceived(f.Peer.Id, "accepted", null);
        await f.Browser.PostMessageAsync("quoted \"payload\""); Assert.Equal("quoted \"payload\"", f.Peer.Message);
        f.Browser.Stop(); f.Peer.Client.WebMessageReceived(f.Peer.Id, "stale", null);
        Assert.Equal(["accepted"], messages);
    }
    [Fact]
    public void DesignerPropertiesExposeSafePrimitiveSerialization()
    {
        using var f = new Fixture(); f.Browser.SourceAddress = "https://design.test/";
        Assert.Equal("design.test", f.Browser.Source!.Host);
        var properties = TypeDescriptor.GetProperties(f.Browser);
        Assert.Equal(DesignerSerializationVisibility.Hidden,
            ((DesignerSerializationVisibilityAttribute)properties[nameof(WebView.Source)]!.Attributes[typeof(DesignerSerializationVisibilityAttribute)]!).Visibility);
        Assert.True(properties[nameof(WebView.SourceAddress)]!.IsBrowsable);
    }
    [Fact]
    public void SequentialLeaseReplacementDisposesEveryOldPeer()
    {
        using var f = new Fixture();
        for (int i = 0; i < 25; ++i) { f.Ready(); f.Window.NativeViews.SetAvailable(false); f.Window.NativeViews.SetAvailable(true); }
        f.Browser.Dispose(); Assert.All(f.Provider.Peers, p => Assert.Equal(1, p.Disposals));
    }
    [Fact]
    public void RawHtmlReloadNormalizesEnginePrivateDataAddress()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.NavigateToHtml("<p>raw</p>");
        f.Peer.Client.NavigationCompleted(f.Peer.Id, null, null);
        Assert.True(f.Browser.Reload());
        Assert.True(f.Peer.Client.NavigationStarting(f.Peer.Id, new Uri("data:text/html,private"), false));
        Assert.Null(f.Browser.Source);
    }
    [Fact]
    public void TerminalCallbacksCompleteRequestExactlyOnce()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.Navigate(new("https://page.test"));
        int completed = 0, failed = 0;
        f.Browser.NavigationCompleted += (_, _) => ++completed;
        f.Browser.NavigationFailed += (_, _) => ++failed;
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        f.Peer.Client.NavigationCompleted(f.Peer.Id, f.Browser.Source, 200);
        f.Peer.Client.NavigationFailed(f.Peer.Id, f.Browser.Source, WebViewErrorCategory.Unknown);
        Assert.Equal(1, completed); Assert.Equal(0, failed);
    }
    [Fact]
    public void PublicSharedContractsReferenceNoBrowserPlatformAssembly()
    {
        var references = typeof(WebView).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, a => a.Name!.Contains("WebView2", StringComparison.Ordinal) || a.Name.Contains("Mono.Android", StringComparison.Ordinal));
        using var f = new Fixture(); Assert.Equal(WebViewCapabilities.None, f.Browser.BrowserCapabilities);
        f.Ready(); Assert.True(f.Browser.BrowserCapabilities.HasFlag(WebViewCapabilities.Messaging));
    }
    [Fact]
    public void DetachedDesignerPaintStartsNoEngineOrNavigation()
    {
        using var app = ModernFormsTestHost.Create();
        var provider = new FakeProvider();
        using var browser = new PreviewWebView(provider)
        { Size = new(200, 100), SourceAddress = "https://design.test/", IsJavaScriptEnabled = true };
        using var bitmap = new SKBitmap(200, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        browser.PaintPreview(new PaintEventArgs(bitmap.Info, canvas, 1d));
        canvas.Flush();
        Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha > 0);
        Assert.Empty(provider.Peers);
        Assert.False(browser.IsInitialized); Assert.Equal("WebView", browser.Text);
    }
    [Fact]
    public void FailureObserverExceptionStillSettlesLoading()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.Navigate(new("https://failure.test"));
        f.Browser.NavigationFailed += (_, _) => throw new InvalidOperationException("observer");
        Assert.Throws<InvalidOperationException>(() => f.Peer.Client.NavigationFailed(f.Peer.Id, f.Browser.Source, WebViewErrorCategory.Network));
        Assert.False(f.Browser.IsLoading); Assert.Equal(WebViewErrorCategory.Network, f.Browser.GetDiagnostics().LastFailure);
    }

    [Theory]
    [InlineData("file:///private")]
    [InlineData("data:text/html,redirect")]
    [InlineData("javascript:alert(1)")]
    [InlineData("custom:external")]
    public void RawHtmlDoesNotNormalizeDisallowedRedirects(string destination)
    {
        using var f = new Fixture(); f.Ready(); f.Browser.NavigateToHtml("<p>raw</p>");
        Assert.False(f.Peer.Client.NavigationStarting(f.Peer.Id, new Uri(destination), true));
        Assert.Equal(WebViewErrorCategory.InvalidAddress, f.Browser.GetDiagnostics().LastFailure);
        Assert.False(f.Browser.IsLoading);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StopAndDisablingJavaScriptCancelOutstandingResults(bool stop)
    {
        Task<string> task;
        using (var f = new Fixture())
        {
            f.Ready(); f.Browser.IsJavaScriptEnabled = true;
            task = f.Browser.ExecuteScriptAsync("1");
            if (stop) f.Browser.Stop(); else f.Browser.IsJavaScriptEnabled = false;
            f.Peer.Script.TrySetResult("42");
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void RetirementStillDisposesPeerWhenProviderCancellationThrows()
    {
        using var f = new Fixture(); f.Ready(); f.Browser.IsJavaScriptEnabled = true;
        CancellationTokenRegistration registration = default;
        f.Peer.ScriptStarted = token => registration = token.Register(() => throw new InvalidOperationException("provider cancellation"));
        _ = f.Browser.ExecuteScriptAsync("1");
        var old = f.Peer;
        try { Assert.Throws<AggregateException>(() => f.Window.NativeViews.SetAvailable(false)); }
        finally { registration.Dispose(); }
        Assert.Equal(1, old.Disposals); Assert.False(old.Client.IsCurrent);
        Assert.Equal(WebViewInitializationState.Detached, f.Browser.InitializationState);
    }

    [Fact]
    public void PublicSharedSignaturesExposeNoNativeEngineOrHandleTypes()
    {
        static void Check(Type type)
        {
            if (type.HasElementType) { Check(type.GetElementType()!); return; }
            Assert.NotEqual(typeof(IntPtr), type); Assert.NotEqual(typeof(UIntPtr), type);
            Assert.DoesNotContain(new[] { "Microsoft.Web.WebView2", "Android", "Java",
                "ModernFormsNext.WebView.Windows", "ModernFormsNext.WebView.Android",
                "ModernFormsNext.WindowKit.Backend.Windows", "ModernFormsNext.WindowKit.Backend.Android" },
                prefix => type.Namespace == prefix || type.Namespace?.StartsWith(prefix + ".", StringComparison.Ordinal) == true);
            foreach (var argument in type.GetGenericArguments()) Check(argument);
        }
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (var type in typeof(WebView).Assembly.GetExportedTypes())
        {
            Check(type);
            foreach (var property in type.GetProperties(flags)) Check(property.PropertyType);
            foreach (var field in type.GetFields(flags)) Check(field.FieldType);
            foreach (var notification in type.GetEvents(flags)) Check(notification.EventHandlerType!);
            foreach (var method in type.GetMethods(flags))
            { Check(method.ReturnType); foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType); }
            foreach (var constructor in type.GetConstructors(flags))
                foreach (var parameter in constructor.GetParameters()) Check(parameter.ParameterType);
        }
    }
}
