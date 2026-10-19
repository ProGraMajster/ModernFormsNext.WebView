using System.ComponentModel;

namespace ModernFormsNext.WebView;

public partial class WebView
{
    private WebViewNewWindowPolicy newWindowPolicy;
    private object newWindowConfiguration = new();
    private bool openedAsNewWindow;
    private const string ScriptPopupWarning = "Security warning: non-gesture popup requests can cause spam/phishing and expose opener/shared-profile data. Prefer UserInitiated or Deny; validate each application-owned target. No caller authentication is granted.";

    /// <summary>Gets or sets per-view native new-window policy; defaults to Deny.</summary>
    /// <remarks>
    /// UI thread only, runtime-mutable without layout/reload, retained across native recreation.
    /// Allowed intents require NewWindowRequested.OpenIn with a fresh ready hosted target; no
    /// OS window, system browser, permission or trusted handler is granted. Native gesture/URI
    /// information is not origin/frame authentication. Android's automatic JS-window flag is
    /// enabled only for numeric value 2, which carries compiler diagnostic MFNWVSEC001.
    /// Dynamic higher-risk changes emit SecurityWarning with fixed redacted text. Invalid values
    /// throw before mutation. Ready unsupported adapters throw NotSupportedException; an unsupported
    /// queued nondefault configuration fails initialization with Engine before navigation replay.
    /// Windows requires initialized WebView2 hooks; Android uses SDK WebChromeClient (API23 floor).
    /// Cookies/storage/opener semantics remain engine/profile-wide where applicable.
    /// </remarks>
    /// <example><code>
    /// browser.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
    /// browser.NewWindowRequested += (_, e) =>
    /// {
    ///     // preparedTarget was separately hosted/initialized and has never been used.
    ///     e.OpenIn(preparedTarget);
    /// };
    /// preparedTarget.CloseRequested += (_, _) => preparedTarget.Dispose();
    /// // A JSON/Enum.Parse selection is checked at runtime; subscribe before configuring it.
    /// browser.SecurityWarning += (_, e) => Log(e.DiagnosticId, e.Message);
    /// </code></example>
    [DefaultValue(WebViewNewWindowPolicy.Deny)]
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WebViewNewWindowPolicy NewWindowPolicy
    {
        get => newWindowPolicy;
        set
        {
            VerifyAccess();
            if ((uint)value > 2) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == newWindowPolicy) return;
            if (IsInitialized && value != WebViewNewWindowPolicy.Deny && !HasNewWindows)
                throw new NotSupportedException("The current native peer does not support explicit new-window handoff.");
            newWindowPolicy = value;
            newWindowConfiguration = new(); // Even a policy round trip invalidates an outstanding decision.
            if (IsInitialized && lease?.Peer is IWebViewNewWindowPeer peer) peer.ApplyNewWindowPolicy(value);
            if ((int)value == 2) SecurityWarning?.Invoke(this, new("MFNWVSEC001", ScriptPopupWarning));
        }
    }

    /// <summary>Occurs synchronously for an allowed untrusted engine intent; no target selection means deny.</summary>
    /// <remarks>UI thread only. Precreate targets; async handlers/retained arguments cannot accept later. No source origin/frame authentication is implied.</remarks>
    public event EventHandler<WebViewNewWindowRequestedEventArgs>? NewWindowRequested;
    /// <summary>Occurs for a script-close intent on a view opened by an accepted handoff.</summary>
    /// <remarks>UI thread only; informational. The app decides whether to remove/dispose the control or close its Form. Nothing closes automatically.</remarks>
    public event EventHandler? CloseRequested;
    /// <summary>Occurs after an accepted higher-risk configuration change, including a dynamic enum selection.</summary>
    /// <remarks>Subscribe before applying configuration. Fixed ID/message only; no sensitive page data. Safe values and equal assignments do not warn.</remarks>
    public event EventHandler<WebViewSecurityWarningEventArgs>? SecurityWarning;

    private bool HasNewWindows => lease?.Peer is IWebViewNewWindowPeer { SupportsNewWindows: true };
    private bool CanApplyNewWindowPolicy => newWindowPolicy == WebViewNewWindowPolicy.Deny || HasNewWindows;

    private void ValidateNewWindowTarget(WebView target, IWebViewNewWindowPeer opener)
    {
        target.VerifyAccess();
        if (ReferenceEquals(target, this) || !target.IsInitialized || target.requestId != 0 || target.requested is not null ||
            target.lease?.Peer is not IWebViewNewWindowPeer { SupportsNewWindows: true } peer || !peer.CanAcceptNewWindowFrom(opener))
            throw new InvalidOperationException("A separate, ready, unused and natively compatible hosted WebView target is required.");
    }

    private WebViewNewWindowTarget? RequestNewWindow(long id, Uri? destination, bool userInitiated)
    {
        if (!IsInitialized || id != requestId || requested is null || newWindowPolicy == WebViewNewWindowPolicy.Deny ||
            newWindowPolicy == WebViewNewWindowPolicy.UserInitiated && !userInitiated ||
            lease?.Peer is not IWebViewNewWindowPeer { SupportsNewWindows: true } opener) return null;
        if (destination is not null)
        {
            try { ValidateUri(destination); } catch (ArgumentException) { return null; }
        }
        var sourceLease = lease; long sourceGeneration = generation;
        var sourceToken = documentLifetime.Token; var configuration = newWindowConfiguration;
        bool SourceCurrent() => !sourceToken.IsCancellationRequested && initializationState == WebViewInitializationState.Ready &&
            ReferenceEquals(lease, sourceLease) && generation == sourceGeneration && requestId == id && ReferenceEquals(newWindowConfiguration, configuration);
        var args = new WebViewNewWindowRequestedEventArgs(destination, userInitiated, target => ValidateNewWindowTarget(target, opener));
        try { NewWindowRequested?.Invoke(this, args); }
        finally { args.Complete(); }
        if (!SourceCurrent() || args.Target is not { } target) return null;
        // Revalidate after application callbacks. A target navigation or retirement must
        // not be relabeled as the popup merely because it was fresh at OpenIn time.
        try { ValidateNewWindowTarget(target, opener); } catch (InvalidOperationException) { return null; }
        var targetLease = target.lease;
        if (!target.NewCommand(false)) return null;
        if (!SourceCurrent() || !ReferenceEquals(target.lease, targetLease))
        {
            // Cancellation observers on even an unused target may change the opener.
            // Settle only the reservation we just made, never a reentrant newer one.
            if (ReferenceEquals(target.lease, targetLease) && target.IsInitialized)
                targetLease!.FailNewWindow(target.requestId, destination);
            return null;
        }
        var request = target.requested! with { Uri = destination };
        target.requested = request; target.source = destination; target.openedAsNewWindow = true;
        target.isLoading = true;
        var targetToken = target.documentLifetime.Token;
        bool TargetCurrent() => !targetToken.IsCancellationRequested && target.IsInitialized &&
            ReferenceEquals(target.lease, targetLease) && target.requestId == request.Id;
        return new((IWebViewNewWindowPeer)targetLease!.Peer, request, () => SourceCurrent() && TargetCurrent(), () =>
        {
            if (TargetCurrent()) targetLease.FailNewWindow(request.Id, destination);
        });
    }
}
