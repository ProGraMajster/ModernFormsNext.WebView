using Browser = global::Android.Webkit.WebView;
using global::Android.Webkit;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.Android;

internal sealed partial class AndroidWebViewPeer
{
    private bool popupTargetUsed, openedPopup;
    private long? preparedPopupId;
    private bool awaitingInitialPopup;
    private readonly HashSet<PopupVisualState> popupVisualStates = [];
    private WebMessagePort? popupPort;
    private PopupPortCallback? popupPortCallback;
    private CancellationToken popupDocumentLifetime;
    private CancellationTokenRegistration popupCancellation;
    private nint PopupContextHandle => site.Context.Handle;
    public bool SupportsNewWindows => Current && view is not null && chromeClient is not null;
    public void ApplyNewWindowPolicy(WebViewNewWindowPolicy policy)
    {
        if ((uint)policy > 2) throw new ArgumentOutOfRangeException(nameof(policy));
        settings = settings with { NewWindowPolicy = policy };
        if (Current && view is not null) view.Settings.JavaScriptCanOpenWindowsAutomatically = (int)policy == 2;
    }
    public bool CanAcceptNewWindowFrom(IWebViewNewWindowPeer opener) => Current && view is not null && !popupTargetUsed && id == 0 &&
        opener is AndroidWebViewPeer other && !ReferenceEquals(this, other) && other.Current &&
        PopupContextHandle != IntPtr.Zero && PopupContextHandle == other.PopupContextHandle;
    public void PrepareNewWindow(WebViewNavigationRequest request)
    {
        if (!Current || view is null || popupTargetUsed) throw new InvalidOperationException("The native target is no longer fresh.");
        popupTargetUsed = openedPopup = true; preparedPopupId = id = request.Id;
        rawHtml = stopped = retireBeforeNextNavigation = false;
        expectedUri = request.Uri; loading = true; navigationClient?.SetContent(null);
        awaitingInitialPopup = true;
    }
    public void CancelNewWindow(long requestId)
    {
        if (preparedPopupId != requestId) return;
        preparedPopupId = null;
        if (id == requestId) Stop(); // Never stop a reentrant newer navigation.
    }
    private bool CreateWindow(bool userInitiated, global::Android.OS.Message? message)
    {
        // Android supplies no destination/origin/frame identity here. Never infer one
        // from HitTestResult, current URL or this native gesture indication.
        if (!Current || callbacks is not IWebViewNewWindowPeerClient callback || message?.Obj is not Browser.WebViewTransport transport) return false;
        var target = callback.NewWindowRequested(id, null, userInitiated);
        if (target is null) return false;
        if (!target.IsCurrent || target.Peer is not AndroidWebViewPeer peer || !peer.CanAcceptNewWindowFrom(this))
        { target.Reject(); return false; }
        try
        {
            peer.PrepareNewWindow(target.Request);
            if (!target.IsCurrent) { target.Reject(); return false; }
            peer.popupDocumentLifetime = target.DocumentCancellationToken;
            long preparedGeneration = peer.browserGeneration;
            long preparedId = target.Request.Id;
            var preparedToken = target.DocumentCancellationToken;
            peer.popupCancellation = preparedToken.Register(() => Dispatcher.UIThread.Post(() =>
            {
                if (!peer.Current || peer.browserGeneration != preparedGeneration || peer.id != preparedId ||
                    peer.popupDocumentLifetime != preparedToken) return;
                peer.ClosePopupPort();
                if (peer.preparedPopupId == peer.id && peer.loading && !peer.stopped) peer.CancelInitialPopup();
            }));
            transport.WebView = peer.view;
            message.SendToTarget();
            var targetView = peer.view!;
            long targetGeneration = peer.browserGeneration;
            // The SDK consumes its transport message on this same UI looper. Queue
            // after that message, then ask the renderer for actual visual readiness.
            // Initial about:blank + opener.document.write may never raise PageFinished.
            targetView.Post(() =>
            {
                if (!peer.Current || peer.browserGeneration != targetGeneration || !ReferenceEquals(peer.view, targetView) ||
                    peer.id != preparedId || !peer.awaitingInitialPopup || peer.stopped) return;
                var visual = new PopupVisualState(peer, targetView, targetGeneration, preparedId);
                peer.popupVisualStates.Add(visual);
                targetView.PostVisualStateCallback(preparedId, visual);
            });
            return true;
        }
        catch (Java.Lang.Exception) { target.Reject(); return false; }
        catch (InvalidOperationException) { target.Reject(); return false; }
    }

    private sealed class PopupVisualState(AndroidWebViewPeer owner, Browser browser, long generation, long requestId) : Browser.VisualStateCallback
    {
        public override void OnComplete(long returnedId)
        {
            try
            {
                if (returnedId != requestId || !owner.Current || owner.browserGeneration != generation ||
                    !ReferenceEquals(owner.view, browser) || owner.id != requestId || owner.stopped ||
                    !owner.awaitingInitialPopup || !owner.loading || browser.Url is not (null or "about:blank")) return;
                owner.awaitingInitialPopup = false;
                // This is a native first-frame confirmation only, never an origin proof.
                // Real URL navigations clear awaitingInitialPopup in the normal callbacks.
                owner.Started("about:blank");
                if (owner.Current && owner.browserGeneration == generation && ReferenceEquals(owner.view, browser) &&
                    owner.id == requestId && !owner.stopped && owner.loading)
                {
                    if (owner.settings.IsJavaScriptEnabled) _ = owner.InitializeBlankPopupMessagesAsync(browser, generation, requestId);
                    else owner.Finished("about:blank");
                }
            }
            finally { owner.popupVisualStates.Remove(this); Dispose(); }
        }
    }

    private async Task InitializeBlankPopupMessagesAsync(Browser browser, long generation, long requestId)
    {
        PopupPortCallback? callback = null;
        bool StillCurrent() => Current && browserGeneration == generation && ReferenceEquals(view, browser) &&
            id == requestId && !stopped && loading && !popupDocumentLifetime.IsCancellationRequested && ReferenceEquals(popupPortCallback, callback);
        try
        {
            // A popup's initial blank document predates attachment of the target Java
            // interfaces. AddJavascriptInterface becomes visible only on a later load.
            // Use the API23 SDK string port for this document, preserving the original
            // envelope/client/request guards. This grants no origin/caller authority.
            var ports = browser.CreateWebMessageChannel();
            if (ports is not { Length: 2 }) throw new InvalidOperationException("The provider did not create a message channel.");
            popupPort = ports[0];
            using var transfer = ports[1];
            callback = new PopupPortCallback(this, generation, requestId);
            popupPortCallback = callback; popupPort.SetWebMessageCallback(callback);
            string bootstrap = "(function(id){function bind(e){if(e.data!=='mfn-popup-port'||!e.ports||!e.ports[0])return;" +
                "removeEventListener('message',bind);var port=e.ports[0];window.modernFormsWebView={postMessage:function(v){" +
                "port.postMessage(JSON.stringify([id,String(v)]));}};port.postMessage('mfn-popup-ready');}" +
                "addEventListener('message',bind);})(" + requestId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ");null";
            await ExecuteScriptAsync(bootstrap, CancellationToken.None);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!StillCurrent()) return;
                // The wildcard deliberately carries untrusted string data to an opaque
                // blank page. A relayed/stolen port is not an authenticated capability.
                using var destination = global::Android.Net.Uri.Parse("*")!;
                using var message = new WebMessage("mfn-popup-port", [transfer]);
                browser.PostWebMessage(message, destination);
            });
            await callback.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!StillCurrent()) return; // Navigation/Stop/retirement owns its newer state.
                ClosePopupPort(); stopped = true; loading = false; retireBeforeNextNavigation = true;
                callbacks.NavigationFailed(requestId, ParseUri(browser.Url), WebViewErrorCategory.Engine);
            });
            return;
        }
        // Observer exceptions follow the ordinary dispatcher policy, outside the
        // recoverable native-bootstrap catch above.
        Dispatcher.UIThread.Post(() => { if (StillCurrent()) Finished("about:blank"); });
    }

    private void ClosePopupPort()
    {
        var previous = popupPort; var callback = popupPortCallback;
        popupPort = null; popupPortCallback = null;
        callback?.Ready.TrySetCanceled();
        // The transferred peer belongs to JavaScript. Close only our native endpoint;
        // queued JNI callbacks retain their wrappers until Chromium releases them.
        try { previous?.Close(); } finally { previous?.Dispose(); }
    }

    private void CancelInitialPopup()
    {
        ClosePopupPort(); stopped = true; loading = false; retireBeforeNextNavigation = true;
        callbacks.NavigationFailed(id, expectedUri, WebViewErrorCategory.Canceled);
    }

    private sealed class PopupPortCallback(AndroidWebViewPeer owner, long generation, long requestId) : WebMessagePort.WebMessageCallback
    {
        internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void OnMessage(WebMessagePort? port, WebMessage? message)
        {
            string? packet = message?.Data;
            Dispatcher.UIThread.Post(() =>
            {
                if (!owner.Current || owner.browserGeneration != generation || owner.id != requestId || owner.stopped || owner.popupDocumentLifetime.IsCancellationRequested ||
                    !ReferenceEquals(owner.popupPortCallback, this)) return;
                if (packet == "mfn-popup-ready") Ready.TrySetResult();
                else owner.ReceiveBridgePacket(packet, generation, owner.popupDocumentLifetime);
            });
        }
    }
}
