using System.Drawing;
using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Windows;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.Windows;

internal sealed partial class WindowsWebViewPeer(WindowsWebViewProvider provider, WindowsNativeViewSite site,
    IWebViewPeerClient client) : IWebViewPeer, IWebViewLocalContentPolicyPeer, IWebViewNavigationFilterPeer, IWebViewNewWindowPeer
{
    private CoreWebView2Controller? controller;
    private CoreWebView2? core;
    private PixelSize size;
    private WebViewSettings settings = new(false, null);
    private string? defaultUserAgent;
    private bool disposed, initializing;
    private long logicalId;
    private NavigationCommandQueue? commands;
    private ulong? activeNativeId;
    private readonly Dictionary<ulong, long> navigations = [];
    private string? bridgeScriptId;
    private WebViewLocalContent? localContent;
    private readonly WindowsMessageGuard messages = new();
    public bool SupportsLocalContent { get; private set; }
    public bool SupportsLocalContentPolicy => SupportsLocalContent;
    // Subscribe must complete before Initialized is reported; no capability survives CloseController.
    public bool SupportsNavigationSchemeFiltering => Current && core is not null;
    private bool Current => !disposed && site.IsCurrent && client.IsCurrent;

    private const string BridgeScript =
        "chrome.webview.addEventListener('message',function(e){window.dispatchEvent(new MessageEvent('message',{data:e.data}));});";
    private static string CreateHelperScript(long requestId, string token) =>
        "window.modernFormsWebView={postMessage:(function(id,token){return function(v){" +
        "chrome.webview.postMessage(JSON.stringify([id,token,String(v)]));};})(" +
        requestId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",'" + token + "')};";

    public void Initialize(WebViewSettings value)
    {
        settings = value;
        if (initializing) return;
        initializing = true;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        CoreWebView2Controller? candidate = null;
        CoreWebView2Environment environment;
        try
        {
            environment = await provider.GetEnvironmentAsync();
            // Use the established dispatcher after every await, even if the caller had no
            // SynchronizationContext while its Form was preparing first display.
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (!Current) return;
                candidate = await environment.CreateCoreWebView2ControllerAsync(site.ParentWindow);
            });
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (candidate is null) return;
                if (!Current) { candidate.Close(); candidate = null; return; }
                controller = candidate; candidate = null; core = controller.CoreWebView2;
                popupEnvironment = environment;
                defaultUserAgent = core.Settings.UserAgent;
                controller.Bounds = new Rectangle(0, 0, size.Width, size.Height);
                controller.IsVisible = true;
                Subscribe(); ApplySettings(settings);
                InstallLocalResolver();
                bridgeScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);
                if (!Current) return;
                // Observer exceptions belong to the normal dispatcher policy, outside the
                // native initialization catch. They must not be mislabeled as engine failures.
                Dispatcher.UIThread.Post(() =>
                {
                    if (!Current) return;
                    if (site.IsFocused) RequestFocus();
                    client.Initialized("Windows", "WebView2", environment.BrowserVersionString);
                });
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try { candidate?.Close(); }
                finally
                {
                    CloseController();
                    Dispatcher.UIThread.Post(() => { if (Current) client.InitializationFailed(WebViewErrorCategory.Engine); });
                }
            });
        }
    }

    public void Resize(PixelSize value)
    {
        size = value;
        if (Current && controller is not null) controller.Bounds = new Rectangle(0, 0, value.Width, value.Height);
    }
    public void RequestFocus() { if (Current && site.IsFocused) controller?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic); }
    // The engine's MoveFocusRequested event identifies the actual HTML Tab boundary.
    // MFN must not treat every Tab as a request to leave the native document.
    public bool TryMoveFocus(bool forward)
    {
        if (!Current || controller is null) return false;
        controller.MoveFocus(forward ? CoreWebView2MoveFocusReason.Next : CoreWebView2MoveFocusReason.Previous);
        return true;
    }
    public void ApplySettings(WebViewSettings value)
    {
        if (!value.IsJavaScriptEnabled) messages.Revoke();
        settings = value; if (!Current || core is null) return;
        core.Settings.IsScriptEnabled = value.IsJavaScriptEnabled;
        core.Settings.IsWebMessageEnabled = value.IsJavaScriptEnabled;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.UserAgent = value.UserAgent ?? defaultUserAgent!;
    }
    public void Navigate(WebViewNavigationRequest request)
    {
        if (!Current || core is null) return;
        popupTargetUsed = true;
        Commands.Submit(request, () =>
        {
            string token = messages.Prepare(request.Id);
            localContent = request.LocalContent;
            if (request.Html is { } html) core.NavigateToString(settings.IsJavaScriptEnabled
                ? "<script>" + CreateHelperScript(request.Id, token) + "</script>" + html : html);
            else core.Navigate(request.Uri?.AbsoluteUri ?? "about:blank");
        });
    }
    public void GoBack(long id) { if (Current && core?.CanGoBack == true) Commands.Submit(new(id, null, null), () => { messages.Prepare(id); core.GoBack(); }); }
    public void GoForward(long id) { if (Current && core?.CanGoForward == true) Commands.Submit(new(id, null, null), () => { messages.Prepare(id); core.GoForward(); }); }
    public void Reload(long id) { if (Current && core is not null) Commands.Submit(new(id, null, null), () => { messages.Prepare(id); core.Reload(); }); }
    private NavigationCommandQueue Commands => commands ??= new(() => Current, action => Dispatcher.UIThread.Post(action));
    public void Stop()
    {
        messages.Revoke();
        activeNativeId = null;
        // An unacknowledged command is canceled by its original-ID Starting callback.
        // Calling Stop before it starts can suppress that callback and strand the queue.
        if (Current && Commands.Stop()) core?.Stop();
    }

    public async Task<string> ExecuteScriptAsync(string script, CancellationToken token)
    {
        if (!Current || core is null) throw new WebViewOperationException(WebViewErrorCategory.Engine);
        popupTargetUsed = true;
        try { return await core.ExecuteScriptAsync(script).WaitAsync(token); }
        catch (OperationCanceledException) { throw; }
        catch { throw new WebViewOperationException(WebViewErrorCategory.Engine); }
    }
    public Task PostMessageAsync(string message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Current || core is null) throw new WebViewOperationException(WebViewErrorCategory.Engine);
        popupTargetUsed = true;
        try { core.PostWebMessageAsString(message); return Task.CompletedTask; }
        catch { throw new WebViewOperationException(WebViewErrorCategory.Engine); }
    }

    private void Starting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Current) { e.Cancel = true; return; }
        WebViewNavigationRequest? submitted = null;
        if (!navigations.TryGetValue(e.NavigationId, out long id))
        {
            submitted = Commands.Pending;
            id = submitted?.Id ?? client.BeginNativeNavigation();
            navigations[e.NavigationId] = id;
            logicalId = id; activeNativeId = e.NavigationId;
            messages.Prepare(id);
        }
        Uri? uri = submitted?.Html is not null ? null : ParseUri(e.Uri);
        try { e.Cancel = !client.NavigationStarting(id, uri, e.IsRedirected); }
        catch { e.Cancel = true; throw; }
        finally
        {
            if (e.Cancel) messages.Revoke(id);
            if (!e.IsRedirected && submitted is not null)
            {
                // Native/user Starting may enqueue another command in an observer.
                // Only acknowledge the command captured before notifying that observer.
                Commands.Acknowledge(submitted.Id);
                if (preparedPopupId == submitted.Id) preparedPopupId = null;
            }
        }
    }
    private void Completed(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!navigations.Remove(e.NavigationId, out long id) || !Current) return;
        if (activeNativeId != e.NavigationId) return;
        if (e.IsSuccess) _ = CompleteSuccessAsync(e.NavigationId, id, e.HttpStatusCode == 0 ? null : e.HttpStatusCode);
        else { messages.Revoke(id); client.NavigationFailed(id, ParseUri(core?.Source), WindowsWebViewPolicy.MapError(e.WebErrorStatus)); }
    }
    private async Task CompleteSuccessAsync(ulong nativeId, long id, int? status)
    {
        // The helper closes over the original request and opaque per-document token.
        // Substituting the public request ID in an old packet cannot make it current.
        // Remote documents get the helper before the public completion notification.
        try
        {
            if (!Current || activeNativeId != nativeId) return;
            if (settings.IsJavaScriptEnabled && core is not null)
            {
                // A stale async completion must not rotate or restore the guard.
                // An already submitted script can only carry its original token.
                // JS can be disabled/re-enabled while this document is loading. Its
                // revoked helper stays unavailable until reload, but navigation must
                // still settle rather than remaining permanently IsLoading.
                if (messages.TryGetToken(id, out string token))
                    await core.ExecuteScriptAsync(CreateHelperScript(id, token));
            }
        }
        catch
        {
            messages.Revoke(id);
            Dispatcher.UIThread.Post(() =>
            {
                if (Current && activeNativeId == nativeId) client.NavigationFailed(id, ParseUri(core?.Source), WebViewErrorCategory.Engine);
            });
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            if (!Current || activeNativeId != nativeId) return;
            UpdateHistory(id);
            client.NavigationCompleted(id, ParseUri(core?.Source), status);
        });
    }
    private void HistoryChanged(object? sender, object e) { if (Current) UpdateHistory(logicalId); }
    private void UpdateHistory(long id) { if (core is not null) client.HistoryChanged(id, core.CanGoBack, core.CanGoForward); }
    private void Message(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Current || !settings.IsJavaScriptEnabled) return;
        string wire;
        try
        {
            wire = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException) { return; }
        var source = ParseUri(e.Source);
        if (!messages.TryRead(wire, source, localContent?.Origin, out long originalId, out string message)) return;
        // This guard never authenticates the script caller. Observer exceptions follow
        // the framework's normal policy, including JSON/argument errors.
        client.WebMessageReceived(originalId, message, source);
    }
    private void Permission(object? sender, CoreWebView2PermissionRequestedEventArgs e) => e.State = CoreWebView2PermissionState.Deny;
    private void ResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var browserCore = core;
        if (browserCore is null) return; // A callback queued before Close cannot reopen a retired engine.
        if (!WebViewLocalContent.IsLocalUri(e.Request.Uri)) return;
        // Never allow retired, unknown or invalid reserved URLs to fall through to DNS.
        var response = Current && localContent is { } content
            ? content.CreateResponse(e.Request.Uri, e.Request.Method) : WebViewLocalContent.ErrorResponse();
        string charset = response.MimeType.StartsWith("image/", StringComparison.Ordinal) ? "" : "; charset=utf-8";
        string headers = "Content-Type: " + response.MimeType + charset + "\r\n" +
            string.Join("\r\n", response.Headers.Select(pair => pair.Key + ": " + pair.Value));
        e.Response = browserCore.Environment.CreateWebResourceResponse(response.OpenRead(), response.StatusCode,
            response.StatusCode == 200 ? "OK" : "Local resource unavailable", headers);
    }
    private void Download(object? sender, CoreWebView2DownloadStartingEventArgs e) => e.Cancel = true;
    private void MoveFocus(object? sender, CoreWebView2MoveFocusRequestedEventArgs e)
    {
        if (!Current) return;
        e.Handled = true;
        if (!site.MoveFocus(e.Reason != CoreWebView2MoveFocusReason.Previous)) site.RestoreFocus();
    }
    private void Subscribe()
    {
        core!.NavigationStarting += Starting; core.NavigationCompleted += Completed;
        core.HistoryChanged += HistoryChanged; core.WebMessageReceived += Message;
        core.PermissionRequested += Permission; core.NewWindowRequested += NewWindow;
        core.WindowCloseRequested += CloseWindow;
        core.DownloadStarting += Download; controller!.MoveFocusRequested += MoveFocus;
        core.WebResourceRequested += ResourceRequested;
    }
    private void CloseController()
    {
        messages.Revoke();
        var previous = controller; controller = null;
        var previousCore = core; core = null;
        try
        {
            if (previousCore is not null)
            {
                previousCore.NavigationStarting -= Starting; previousCore.NavigationCompleted -= Completed;
                previousCore.HistoryChanged -= HistoryChanged; previousCore.WebMessageReceived -= Message;
                previousCore.PermissionRequested -= Permission; previousCore.NewWindowRequested -= NewWindow;
                previousCore.WindowCloseRequested -= CloseWindow;
                previousCore.DownloadStarting -= Download;
                previousCore.WebResourceRequested -= ResourceRequested;
                if (bridgeScriptId is not null) previousCore.RemoveScriptToExecuteOnDocumentCreated(bridgeScriptId);
            }
        }
        finally
        {
            if (previous is not null) { previous.MoveFocusRequested -= MoveFocus; previous.Close(); }
            bridgeScriptId = null; navigations.Clear(); commands?.Clear();
            localContent = null; SupportsLocalContent = false; popupEnvironment = null; preparedPopupId = null;
        }
    }
    public void Dispose() { if (disposed) return; disposed = true; CloseController(); }
    private static Uri? ParseUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
