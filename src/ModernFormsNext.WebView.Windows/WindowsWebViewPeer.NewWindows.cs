using Microsoft.Web.WebView2.Core;

namespace ModernFormsNext.WebView.Windows;

internal sealed partial class WindowsWebViewPeer
{
    private CoreWebView2Environment? popupEnvironment;
    private bool popupTargetUsed, openedPopup;
    private long? preparedPopupId;
    public bool SupportsNewWindows => Current && core is not null;
    public void ApplyNewWindowPolicy(WebViewNewWindowPolicy policy)
    {
        if ((uint)policy > 2) throw new ArgumentOutOfRangeException(nameof(policy));
        settings = settings with { NewWindowPolicy = policy };
        // WebView2 requests are filtered by the shared synchronous decision. No other
        // setting is reapplied, so a popup policy change cannot reload a user agent.
    }
    public bool CanAcceptNewWindowFrom(IWebViewNewWindowPeer opener) => Current && core is not null &&
        !popupTargetUsed && preparedPopupId is null && Commands.Pending is null && navigations.Count == 0 &&
        opener is WindowsWebViewPeer other && !ReferenceEquals(this, other) && other.Current &&
        popupEnvironment is not null && ReferenceEquals(popupEnvironment, other.popupEnvironment);

    public void PrepareNewWindow(WebViewNavigationRequest request)
    {
        if (!Current || core is null || popupTargetUsed) throw new InvalidOperationException("The native target is no longer fresh.");
        popupTargetUsed = openedPopup = true; preparedPopupId = request.Id;
        localContent = null;
        // Reserve the original canonical ID before the SDK can report Starting. A
        // reentrant app command queues behind this acknowledgement rather than being
        // mistaken for the earlier popup's native navigation.
        Commands.Submit(request, () => messages.Prepare(request.Id));
    }
    public void CancelNewWindow(long id)
    {
        if (preparedPopupId != id) return;
        preparedPopupId = null; messages.Revoke(id);
        Commands.Acknowledge(id); // Drain a newer queued command without stopping it.
    }
    private void NewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true; // Set before invoking any application callback, including one which throws.
        if (!Current || client is not IWebViewNewWindowPeerClient callback) return;
        var target = callback.NewWindowRequested(logicalId, ParseUri(e.Uri), e.IsUserInitiated);
        if (target is null) return;
        if (!target.IsCurrent || target.Peer is not WindowsWebViewPeer peer || !peer.CanAcceptNewWindowFrom(this))
        { target.Reject(); return; }
        try
        {
            peer.PrepareNewWindow(target.Request);
            if (!target.IsCurrent) { target.Reject(); return; }
            e.NewWindow = peer.core;
            // WebView2 replaces the target web contents. Its documented resource
            // interception registration belongs after NewWindow assignment, whereas
            // the already-awaited document-created bridge registration belongs before.
            peer.InstallLocalResolver();
        }
        catch (System.Runtime.InteropServices.COMException) { target.Reject(); }
        catch (InvalidOperationException) { target.Reject(); }
    }
    private void CloseWindow(object? sender, object e)
    {
        if (Current && openedPopup && client is IWebViewNewWindowPeerClient callback)
            callback.CloseRequested(logicalId);
    }
    private void InstallLocalResolver()
    {
        if (core is null) return;
        core.WebResourceRequested -= ResourceRequested;
        core.WebResourceRequested += ResourceRequested;
        try
        {
            // SourceKinds.Document covers pages/frames/dedicated workers. Do not
            // subscribe shared/service workers once for every controller.
            core.AddWebResourceRequestedFilter("*://*mfn-app.invalid*", CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.Document);
            SupportsLocalContent = true;
        }
        catch (NotImplementedException) { SupportsLocalContent = false; }
        catch (System.Runtime.InteropServices.COMException) { SupportsLocalContent = false; }
    }
}
