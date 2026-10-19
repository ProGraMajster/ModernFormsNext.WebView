using Browser = global::Android.Webkit.WebView;

namespace ModernFormsNext.WebView.Android;

internal sealed partial class AndroidWebViewPeer
{
    private bool popupTargetUsed, openedPopup;
    private long? preparedPopupId;
    private bool awaitingInitialPopup;
    private readonly HashSet<PopupVisualState> popupVisualStates = [];
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
                    peer.id != target.Request.Id || !peer.awaitingInitialPopup || peer.stopped) return;
                var visual = new PopupVisualState(peer, targetView, targetGeneration, target.Request.Id);
                peer.popupVisualStates.Add(visual);
                targetView.PostVisualStateCallback(target.Request.Id, visual);
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
                    owner.id == requestId && !owner.stopped && owner.loading) owner.Finished("about:blank");
            }
            finally { owner.popupVisualStates.Remove(this); Dispose(); }
        }
    }
}
