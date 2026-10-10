using System.Text.Json;
using global::Android.Webkit;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Threading;
using Browser = global::Android.Webkit.WebView;

namespace ModernFormsNext.WebView.Android;

internal sealed class AndroidWebViewPeer(AndroidNativeViewSite site, IWebViewPeerClient client) : IWebViewPeer
{
    private readonly IWebViewPeerClient callbacks = client;
    private Browser? view;
    private AndroidViewPeer? attachment;
    private NavigationClient? navigationClient;
    private ChromeClient? chromeClient;
    private MessageBridge? bridge;
    private string? defaultUserAgent;
    private WebViewSettings settings = new(false, null);
    private long id;
    private Uri? expectedUri;
    private bool disposed, rawHtml, stopped;
    private bool loading;
    private long browserGeneration;
    private PixelSize contentSize;
    private bool Current => !disposed && site.IsCurrent && callbacks.IsCurrent;
    private const string BridgeName = "__modernFormsStringBridge";
    private static string CreateHelperScript(long requestId) =>
        "window.modernFormsWebView={postMessage:(function(id){return function(v){" + BridgeName +
        ".postMessage(JSON.stringify([id,String(v)]));};})(" + requestId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")};";
    private readonly HashSet<Evaluation> evaluations = [];

    public void Initialize(WebViewSettings value)
    {
        if (view is not null || !Current) return;
        settings = value;
        try
        {
            view = new Browser(site.Context);
            defaultUserAgent = view.Settings.UserAgentString;
            // Baseline never grants local file/content access, mixed content or media permissions.
            view.Settings.AllowFileAccess = false;
            view.Settings.AllowContentAccess = false;
            view.Settings.AllowFileAccessFromFileURLs = false;
            view.Settings.AllowUniversalAccessFromFileURLs = false;
            view.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
            view.Settings.SetSupportMultipleWindows(true);
            view.Settings.JavaScriptCanOpenWindowsAutomatically = false;
            view.Settings.MediaPlaybackRequiresUserGesture = true;
            navigationClient = new NavigationClient(this, ++browserGeneration); chromeClient = new ChromeClient();
            view.SetWebViewClient(navigationClient); view.SetWebChromeClient(chromeClient);
            attachment = new AndroidViewPeer(site, view, ownsView: false);
            ApplySettings(value);
        }
        catch
        {
            Cleanup();
            if (Current) callbacks.InitializationFailed(WebViewErrorCategory.Engine);
            return;
        }
        string? version = null;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var package = Browser.CurrentWebViewPackage;
            version = package is null ? null : package.PackageName + "/" + package.VersionName;
        }
        callbacks.Initialized("Android", "android.webkit.WebView", version);
        if (Current && site.IsFocused) RequestFocus();
    }

    public void ApplySettings(WebViewSettings value)
    {
        settings = value;
        if (view is null || !Current) return;
        view.Settings.JavaScriptEnabled = value.IsJavaScriptEnabled;
        view.Settings.UserAgentString = value.UserAgent ?? defaultUserAgent;
        if (value.IsJavaScriptEnabled && bridge is null)
        {
            bridge = new MessageBridge(this, browserGeneration);
            view.AddJavascriptInterface(bridge, BridgeName);
        }
        else if (!value.IsJavaScriptEnabled && bridge is not null)
        {
            view.RemoveJavascriptInterface(BridgeName); bridge.Dispose(); bridge = null;
        }
    }
    public void Resize(PixelSize size) { contentSize = size; attachment?.Resize(size); }
    public void RequestFocus() { if (Current && site.IsFocused) attachment?.RequestFocus(); }
    public bool TryMoveFocus(bool forward)
    {
        if (!Current || view is null) return false;
        // HTML focusable elements are virtual Chromium nodes, not Android View children.
        // Let the browser handle Tab itself; the framework site handles the boundary only
        // when the native browser declines it. No DOM/JavaScript focus manager is added.
        long now = global::Android.OS.SystemClock.UptimeMillis();
        var modifiers = forward ? global::Android.Views.MetaKeyStates.None : global::Android.Views.MetaKeyStates.ShiftOn;
        using var down = new global::Android.Views.KeyEvent(now, now, global::Android.Views.KeyEventActions.Down,
            global::Android.Views.Keycode.Tab, 0, modifiers);
        using var up = new global::Android.Views.KeyEvent(now, now, global::Android.Views.KeyEventActions.Up,
            global::Android.Views.Keycode.Tab, 0, modifiers);
        bool handled = view.DispatchKeyEvent(down);
        view.DispatchKeyEvent(up);
        return handled;
    }
    private void MoveFocusAtBoundary(bool forward)
    { if (Current && site.IsFocused && !site.MoveFocus(forward)) site.RestoreFocus(); }
    public void Navigate(WebViewNavigationRequest request)
    {
        if (!Current || view is null) return;
        long previousId = id;
        id = request.Id;
        if (!callbacks.NavigationStarting(id, request.Uri, false) || !Current) return;
        if (loading && previousId != request.Id)
        {
            // Android supplies no native navigation identity. An interrupted document gets
            // a new browser content instance so callbacks from the replaced request cannot
            // be relabeled with the new ID. This deliberately resets native history on
            // overlapping navigation; the backend-owned site/container remains unchanged.
            ReplaceInterruptedBrowser();
            if (!Current || view is null || id != request.Id) return;
            callbacks.HistoryChanged(request.Id, false, false);
            if (!Current || id != request.Id) return;
        }
        id = request.Id; expectedUri = request.Uri; rawHtml = request.Html is not null; stopped = false;
        loading = true;
        if (request.Html is { } html)
            view.LoadDataWithBaseURL(null, settings.IsJavaScriptEnabled ? "<script>" + CreateHelperScript(id) + "</script>" + html : html, "text/html", "UTF-8", null);
        else view.LoadUrl(request.Uri?.AbsoluteUri ?? "about:blank");
    }
    public void GoBack(long requestId) { id = requestId; rawHtml = stopped = false; loading = true; expectedUri = null; if (Current) view?.GoBack(); }
    public void GoForward(long requestId) { id = requestId; rawHtml = stopped = false; loading = true; expectedUri = null; if (Current) view?.GoForward(); }
    public void Reload(long requestId)
    {
        id = requestId; stopped = false;
        Uri? uri = rawHtml ? null : ParseUri(view?.Url);
        if (Current && callbacks.NavigationStarting(id, uri, false) && Current)
        { expectedUri = uri; loading = true; view?.Reload(); }
    }
    public void Stop() { stopped = true; loading = false; if (Current) view?.StopLoading(); }
    private void ReplaceInterruptedBrowser()
    {
        Cleanup();
        try
        {
            view = new Browser(site.Context);
            view.Settings.AllowFileAccess = view.Settings.AllowContentAccess = false;
            view.Settings.AllowFileAccessFromFileURLs = view.Settings.AllowUniversalAccessFromFileURLs = false;
            view.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
            view.Settings.SetSupportMultipleWindows(true);
            view.Settings.JavaScriptCanOpenWindowsAutomatically = false;
            view.Settings.MediaPlaybackRequiresUserGesture = true;
            navigationClient = new NavigationClient(this, ++browserGeneration); chromeClient = new ChromeClient();
            view.SetWebViewClient(navigationClient); view.SetWebChromeClient(chromeClient);
            attachment = new AndroidViewPeer(site, view, ownsView: false);
            attachment.Resize(contentSize); ApplySettings(settings);
            if (site.IsFocused) attachment.RequestFocus();
        }
        catch
        {
            Cleanup();
            if (Current) callbacks.InitializationFailed(WebViewErrorCategory.Engine);
        }
    }

    public Task<string> ExecuteScriptAsync(string script, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Current || view is null) throw new WebViewOperationException(WebViewErrorCategory.Engine);
        var evaluation = new Evaluation(this, token); evaluations.Add(evaluation);
        try { view.EvaluateJavascript(script, evaluation); }
        catch { evaluation.Fail(); }
        return evaluation.Task;
    }
    public async Task PostMessageAsync(string message, CancellationToken token)
        => await ExecuteScriptAsync(AndroidWebViewPolicy.CreateMessageScript(message), token);

    private bool Override(Uri? uri)
    {
        if (!Current) return true;
        // A URI leaving raw HTML is a new document, including a link/meta refresh
        // during its load. Do not normalize its destination as the raw HTML's private URL.
        bool redirect = !rawHtml && !stopped && loading && id != 0;
        if (!redirect) id = callbacks.BeginNativeNavigation();
        if (!callbacks.NavigationStarting(id, uri, redirect)) return true;
        rawHtml = false; stopped = false; expectedUri = uri; loading = true;
        return false;
    }
    private void Started(string? url)
    {
        if (!Current || stopped) return;
        var uri = ParseUri(url);
        if (!loading || id == 0)
        {
            // POST and some engine navigations skip ShouldOverrideUrlLoading. Once the
            // previous load finished they need a fresh identity, including from raw HTML.
            // This notification is a stop fallback, not a pre-network cancellation hook.
            id = callbacks.BeginNativeNavigation(); rawHtml = false;
        }
        if (!rawHtml && expectedUri is null)
        {
            long originalId = id;
            var originalView = view;
            expectedUri = uri;
            if (!callbacks.NavigationStarting(originalId, uri, false))
            {
                // An observer may have submitted a new navigation or replaced this View.
                // Stop only the rejected original flow, never that newer document.
                if (Current && id == originalId && ReferenceEquals(view, originalView))
                { originalView?.StopLoading(); stopped = true; loading = false; }
                return;
            }
            if (Current && id == originalId) loading = true;
        }
    }
    private void Finished(string? url)
    {
        if (!Current || stopped || view is null) return;
        Uri? uri = rawHtml ? null : ParseUri(url);
        if (!rawHtml && expectedUri is not null && uri != expectedUri) return;
        long originalId = id;
        loading = false; expectedUri = null; // Commit before observers may submit a new request.
        if (settings.IsJavaScriptEnabled) view.EvaluateJavascript(CreateHelperScript(originalId), null);
        callbacks.HistoryChanged(originalId, view.CanGoBack(), view.CanGoForward());
        callbacks.NavigationCompleted(originalId, uri, null);
    }
    private void Failed(string? url, int error)
    {
        if (!Current || stopped) return;
        var uri = ParseUri(url);
        if (!rawHtml && expectedUri is not null && uri != expectedUri) return;
        stopped = true;
        loading = false;
        callbacks.NavigationFailed(id, rawHtml ? null : uri, AndroidWebViewPolicy.MapError(error));
    }
    public void Dispose() { if (disposed) return; disposed = true; Cleanup(); }
    private void Cleanup()
    {
        ++browserGeneration; // Revoke native callbacks before detaching/destroying the browser.
        var previous = view; view = null;
        try
        {
            foreach (var evaluation in evaluations.ToArray()) evaluation.Cancel();
            if (previous is not null)
            {
                previous.RemoveJavascriptInterface(BridgeName);
                // Android accepts null to restore its default client despite the binding's annotation.
                previous.SetWebViewClient(null!); previous.SetWebChromeClient(null); previous.StopLoading();
            }
        }
        finally
        {
            try { attachment?.Dispose(); }
            finally
            {
                attachment = null;
                try { previous?.Destroy(); }
                finally
                {
                    previous?.Dispose(); navigationClient?.Dispose(); chromeClient?.Dispose(); bridge?.Dispose();
                    navigationClient = null; chromeClient = null; bridge = null;
                }
            }
        }
    }
    private static Uri? ParseUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;

    private sealed class NavigationClient(AndroidWebViewPeer owner, long generation) : WebViewClient
    {
        private bool Current => owner.Current && generation == owner.browserGeneration;
        public override bool ShouldOverrideUrlLoading(Browser? view, IWebResourceRequest? request)
            => !Current || request?.IsForMainFrame == true && owner.Override(ParseUri(request.Url?.ToString()));
        public override bool ShouldOverrideUrlLoading(Browser? view, string? url) => !Current || owner.Override(ParseUri(url));
        public override void OnPageStarted(Browser? view, string? url, global::Android.Graphics.Bitmap? favicon) { if (Current) owner.Started(url); }
        public override void OnPageFinished(Browser? view, string? url) { if (Current) owner.Finished(url); }
        public override void OnUnhandledKeyEvent(Browser? view, global::Android.Views.KeyEvent? e)
        {
            // Chromium may decline a key asynchronously, after DispatchKeyEvent returned.
            // Route only its actual Tab boundary through the existing canonical focus site.
            if (e?.KeyCode == global::Android.Views.Keycode.Tab)
            {
                if (Current && e.Action == global::Android.Views.KeyEventActions.Down) owner.MoveFocusAtBoundary(!e.IsShiftPressed);
                return;
            }
            base.OnUnhandledKeyEvent(view, e);
        }
        public override void OnReceivedError(Browser? view, IWebResourceRequest? request, WebResourceError? error)
        { if (Current && request?.IsForMainFrame == true && error is not null) owner.Failed(request.Url?.ToString(), (int)error.ErrorCode); }
        // The legacy callback (API23 remains supported) reports main-frame errors only.
        public override void OnReceivedError(Browser? view, ClientError errorCode, string? description, string? failingUrl)
        { if (Current) owner.Failed(failingUrl, (int)errorCode); }
        public override void OnReceivedSslError(Browser? view, SslErrorHandler? handler, global::Android.Net.Http.SslError? error)
        {
            handler?.Cancel();
            // SSL callbacks do not provide IsForMainFrame. Keep default denial for all
            // resources; only the current main document address can fail its navigation.
            if (Current && !owner.rawHtml && ParseUri(error?.Url) == ParseUri(view?.Url)) owner.Failed(error?.Url, -11);
        }
    }
    private sealed class ChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest? request) => request?.Deny();
        public override void OnGeolocationPermissionsShowPrompt(string? origin, GeolocationPermissions.ICallback? callback)
            => callback?.Invoke(origin, false, false);
        public override bool OnCreateWindow(Browser? view, bool isDialog, bool isUserGesture, global::Android.OS.Message? resultMsg) => false;
        public override bool OnShowFileChooser(Browser? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
        { filePathCallback?.OnReceiveValue(null); return true; }
    }
    private sealed class MessageBridge(AndroidWebViewPeer owner, long generation) : Java.Lang.Object
    {
        [JavascriptInterface, Java.Interop.Export("postMessage")]
        public void PostMessage(string? message)
        {
            if (message is null) return;
            long requestId;
            string payload;
            try
            {
                using var packet = JsonDocument.Parse(message);
                if (packet.RootElement.ValueKind != JsonValueKind.Array || packet.RootElement.GetArrayLength() != 2 ||
                    packet.RootElement[0].ValueKind != JsonValueKind.Number ||
                    !packet.RootElement[0].TryGetInt64(out requestId) || packet.RootElement[1].ValueKind != JsonValueKind.String) return;
                payload = packet.RootElement[1].GetString()!;
            }
            catch (JsonException) { return; }
            // JavascriptInterface runs on Chromium's bridge thread. Capture only the string;
            // resolve current UI state after dispatch, and never claim sender origin on Android.
            Dispatcher.UIThread.Post(() =>
            {
                if (owner.Current && generation == owner.browserGeneration && owner.settings.IsJavaScriptEnabled)
                    owner.callbacks.WebMessageReceived(requestId, payload, null);
            });
        }
    }
    private sealed class Evaluation : Java.Lang.Object, IValueCallback
    {
        private readonly AndroidWebViewPeer owner;
        private readonly TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration registration;
        private bool finished;
        public Evaluation(AndroidWebViewPeer owner, CancellationToken token)
        {
            this.owner = owner;
            registration = token.Register(() =>
            {
                completion.TrySetCanceled(token);
                Dispatcher.UIThread.Post(Cancel);
            });
        }
        public Task<string> Task => completion.Task;
        public void OnReceiveValue(Java.Lang.Object? value)
        {
            if (finished) return;
            if (!owner.Current) { Cancel(); return; }
            try { completion.TrySetResult(value?.ToString() ?? "null"); }
            finally { Release(); }
        }
        public void Fail() { completion.TrySetException(new WebViewOperationException(WebViewErrorCategory.Engine)); Release(); }
        public void Cancel() { completion.TrySetCanceled(); Release(); }
        private void Release()
        {
            if (finished) return; finished = true;
            registration.Dispose(); owner.evaluations.Remove(this);
            // WebView owns the JNI callback reference until Chromium returns. Disposing a
            // callback wrapper before that callback can arrive would invalidate its peer.
        }
    }
}
