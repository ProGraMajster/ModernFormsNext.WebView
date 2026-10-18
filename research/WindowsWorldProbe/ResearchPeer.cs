using System.Drawing;
using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WebView.Windows;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.WorldProbe;

// Deliberately bounded research SDK fixture: no product adapter/Core exposure or
// reflection. NativeViewHost still owns its site, placement, focus and peer lifetime.
internal sealed class ResearchProvider(string profile) : IWebViewPeerFactoryProvider
{
    private Task<CoreWebView2Environment>? environment;
    internal ResearchPeer Peer { get; private set; } = null!;
    internal Task<CoreWebView2Environment> EnvironmentAsync() =>
        environment ??= CoreWebView2Environment.CreateAsync(userDataFolder: profile);
    public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (site is not WindowsNativeViewSite windows || !site.IsCurrent)
            throw new ArgumentException("A current Windows native site is required.", nameof(site));
        return Peer = new ResearchPeer(this, windows, client);
    }
}

internal sealed class ResearchPeer(ResearchProvider provider, WindowsNativeViewSite site,
    IWebViewPeerClient client) : IWebViewPeer
{
    private CoreWebView2Controller? controller;
    private CoreWebView2? core;
    private PixelSize size;
    private bool disposed, initialized;
    private readonly Dictionary<ulong, long> navigations = [];
    private NavigationCommandQueue? queue;
    private NavigationCommandQueue Queue => queue ??= new(() => Current, action => Dispatcher.UIThread.Post(action));
    private ulong? active;
    private long logicalId;
    internal bool Current => !disposed && site.IsCurrent && client.IsCurrent;
    internal CoreWebView2 Core => Current && core is { } value ? value : throw new OperationCanceledException("Retired research peer.");

    public void Initialize(WebViewSettings settings)
    {
        if (initialized) return;
        initialized = true; _ = InitializeAsync(settings);
    }
    private async Task InitializeAsync(WebViewSettings settings)
    {
        CoreWebView2Controller? candidate = null;
        try
        {
            var environment = await provider.EnvironmentAsync();
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Current) candidate = await environment.CreateCoreWebView2ControllerAsync(site.ParentWindow);
            });
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!Current) { candidate?.Close(); candidate = null; return; }
                controller = candidate!; candidate = null; core = controller.CoreWebView2;
                controller.Bounds = new Rectangle(0, 0, size.Width, size.Height); controller.IsVisible = true;
                core.NavigationStarting += Starting; core.NavigationCompleted += Completed;
                core.WebMessageReceived += Message; core.PermissionRequested += Permission;
                core.NewWindowRequested += NewWindow; core.DownloadStarting += Download;
                controller.MoveFocusRequested += MoveFocus;
                ApplySettings(settings);
                Dispatcher.UIThread.Post(() => { if (Current) client.Initialized("Windows", "WebView2 research only", environment.BrowserVersionString); });
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                candidate?.Close(); Close();
                if (Current) client.InitializationFailed(WebViewErrorCategory.Engine);
            });
        }
    }
    public void Resize(PixelSize value)
    {
        size = value;
        if (Current && controller is not null) controller.Bounds = new Rectangle(0, 0, value.Width, value.Height);
    }
    public void RequestFocus() { if (Current && site.IsFocused) controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic); }
    public bool TryMoveFocus(bool forward)
    {
        if (!Current || controller is null) return false;
        controller.MoveFocus(forward ? CoreWebView2MoveFocusReason.Next : CoreWebView2MoveFocusReason.Previous); return true;
    }
    private void MoveFocus(object? sender, CoreWebView2MoveFocusRequestedEventArgs e)
    {
        e.Handled = true;
        if (Current && !site.MoveFocus(e.Reason != CoreWebView2MoveFocusReason.Previous)) site.RestoreFocus();
    }
    public void ApplySettings(WebViewSettings settings)
    {
        if (settings.UserAgent is not null) throw new NotSupportedException("User-agent qualification belongs to the product adapter.");
        if (core is null || !Current) return;
        core.Settings.IsScriptEnabled = settings.IsJavaScriptEnabled;
        core.Settings.IsWebMessageEnabled = settings.IsJavaScriptEnabled;
        core.Settings.AreHostObjectsAllowed = false; core.Settings.AreDevToolsEnabled = false;
    }
    // The fixture supports sequential raw/URI navigation, not product local-content
    // delivery or user-agent settings. Those stay in the separately tested real adapters.
    public void Navigate(WebViewNavigationRequest request)
    {
        if (request.LocalContent is not null) throw new NotSupportedException("No research local-content adapter.");
        Queue.Submit(request, () => { if (request.Html is { } html) Core.NavigateToString(html); else Core.Navigate(request.Uri!.AbsoluteUri); });
    }
    public void Reload(long id) => Queue.Submit(new(id, null, null), () => Core.Reload());
    public void GoBack(long id) => Queue.Submit(new(id, null, null), () => Core.GoBack());
    public void GoForward(long id) => Queue.Submit(new(id, null, null), () => Core.GoForward());
    public void Stop() { active = null; if (Current && Queue.Stop()) core?.Stop(); }
    public Task<string> ExecuteScriptAsync(string script, CancellationToken token) => Core.ExecuteScriptAsync(script).WaitAsync(token);
    public Task PostMessageAsync(string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Core.PostWebMessageAsString(message); return Task.CompletedTask;
    }
    private void Starting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Current) { e.Cancel = true; return; }
        WebViewNavigationRequest? submitted = null;
        if (!navigations.TryGetValue(e.NavigationId, out long id))
        {
            submitted = Queue.Pending;
            id = submitted?.Id ?? client.BeginNativeNavigation();
            navigations[e.NavigationId] = id; logicalId = id; active = e.NavigationId;
        }
        try { e.Cancel = !client.NavigationStarting(id, submitted?.Html is not null ? null : ParseUri(e.Uri), e.IsRedirected); }
        catch { e.Cancel = true; throw; }
        finally { if (!e.IsRedirected && submitted is not null) Queue.Acknowledge(submitted.Id); }
    }
    private void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!navigations.Remove(e.NavigationId, out long id) || !Current || active != e.NavigationId) return;
        // Raw HTML Reload can complete with an empty SDK Source. Preserve null
        // using the product adapter's URI convention instead of throwing in a callback.
        if (e.IsSuccess) client.NavigationCompleted(id, ParseUri(Core.Source), e.HttpStatusCode == 0 ? null : e.HttpStatusCode);
        else client.NavigationFailed(id, ParseUri(Core.Source), WebViewErrorCategory.Engine);
    }
    private void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Current) return;
        string message;
        try { message = e.TryGetWebMessageAsString(); } catch (ArgumentException) { return; }
        // This is intentionally raw, unprivileged diagnostics. It neither authenticates
        // a document nor feeds the isolated-world CDP receiver.
        if (message.Length <= 1024) client.WebMessageReceived(logicalId, message, ParseUri(e.Source));
    }
    private void Permission(object? sender, CoreWebView2PermissionRequestedEventArgs e) => e.State = CoreWebView2PermissionState.Deny;
    private void NewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e) => e.Handled = true;
    private void Download(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;
    private void Close()
    {
        var previous = controller; var previousCore = core; controller = null; core = null;
        try
        {
            if (previousCore is not null)
            {
                previousCore.NavigationStarting -= Starting; previousCore.NavigationCompleted -= Completed;
                previousCore.WebMessageReceived -= Message; previousCore.PermissionRequested -= Permission;
                previousCore.NewWindowRequested -= NewWindow; previousCore.DownloadStarting -= Download;
            }
        }
        finally
        {
            if (previous is not null) { previous.MoveFocusRequested -= MoveFocus; previous.Close(); }
            queue?.Clear(); navigations.Clear(); active = null;
        }
    }
    public void Dispose() { if (!disposed) { disposed = true; Close(); } }
    private static Uri? ParseUri(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
