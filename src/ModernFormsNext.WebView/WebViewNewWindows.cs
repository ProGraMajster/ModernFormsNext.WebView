namespace ModernFormsNext.WebView;

/// <summary>Chooses which native new-window intents this WebView offers to its application.</summary>
/// <remarks>
/// Per control, mutable on its UI thread, retained across peer recreation. Deny is the unchanged
/// default. Every allowed intent still needs a synchronous app decision and a fresh hosted target.
/// Native gesture/URI metadata does not authenticate an origin, frame or JavaScript caller.
/// This does not change TLS, permissions, CSP, navigation schemes or messaging authority.
/// </remarks>
public enum WebViewNewWindowPolicy
{
    /// <summary>Suppresses every new-window intent without raising an application decision.</summary>
    Deny = 0,
    /// <summary>Offers only intents carrying the engine's user-gesture indication.</summary>
    /// <remarks>The indication is not authentication; Android cannot identify the requesting frame/origin.</remarks>
    UserInitiated = 1,
    /// <summary>Also offers script/timer intents without a native user gesture; explicit higher-risk opt-in.</summary>
    /// <remarks>
    /// Enables popup spam/phishing and opener/shared-profile data exposure if the app accepts requests.
    /// Prefer UserInitiated or Deny. This Obsolete warning is a security signal, not removal/deprecation.
    /// Locally suppress MFNWVSEC001 only around a reviewed use; dynamic values also raise SecurityWarning.
    /// Android permits automatic JavaScript window requests, still subject to an explicit owned target.
    /// </remarks>
    [Obsolete("Security warning: permits popup requests without a native user gesture; popup spam, phishing and opener/shared-profile data exposure remain possible. Prefer UserInitiated or Deny; each target still needs explicit application approval.", false, DiagnosticId = "MFNWVSEC001")]
    AllowScriptInitiated = 2
}

/// <summary>Reports a validated higher-risk configuration change without page data.</summary>
/// <remarks>UI thread only. Contains fixed diagnostics, never URLs, scripts, payloads or profile paths.</remarks>
public sealed class WebViewSecurityWarningEventArgs : EventArgs
{
    internal WebViewSecurityWarningEventArgs(string id, string message) { DiagnosticId = id; Message = message; }
    /// <summary>Gets the stable compiler/runtime security diagnostic ID.</summary>
    public string DiagnosticId { get; }
    /// <summary>Gets the fixed risk explanation and safer alternative.</summary>
    public string Message { get; }
}

/// <summary>Offers an untrusted native window intent for a synchronous application decision.</summary>
/// <remarks>
/// No decision means deny. Do not await or retain these arguments. Prepare/host/initialize a fresh
/// target before the intent. The target is consumed once; no OS window is created automatically.
/// Android supplies no URI or reliable origin/frame. Windows URI is destination information only.
/// SDK opener semantics and shared profile/cookies remain; no per-window privacy isolation is promised.
/// </remarks>
public sealed class WebViewNewWindowRequestedEventArgs : EventArgs
{
    private readonly Action<WebView> validate;
    private bool active = true;
    internal WebView? Target { get; private set; }
    internal WebViewNewWindowRequestedEventArgs(Uri? uri, bool userInitiated, Action<WebView> validate)
    { RequestedUri = uri; IsUserInitiated = userInitiated; this.validate = validate; }
    /// <summary>Gets the informational absolute destination, or null when unavailable (including Android).</summary>
    /// <remarks>May contain sensitive query data. It never proves source origin or script identity.</remarks>
    public Uri? RequestedUri { get; }
    /// <summary>Gets the native engine gesture indication; this is not caller authentication.</summary>
    public bool IsUserInitiated { get; }
    /// <summary>Selects an already initialized, unused, separately hosted target on the owning UI thread.</summary>
    /// <remarks>
    /// Call at most once during the event. Windows requires the same provider environment/profile;
    /// Android requires the same Activity context. Prior navigation, scripts/messages, disposal,
    /// unsupported/incompatible peers or selecting the source throw before handoff. Application
    /// navigation/Stop/disposal during the callback can still invalidate and deny the decision.
    /// Local catalogs are not inherited; reserved resource URLs remain denied without a catalog.
    /// The app owns target/control/window lifetime and should handle its CloseRequested event.
    /// </remarks>
    public void OpenIn(WebView target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!active || Target is not null) throw new InvalidOperationException("Select one target synchronously during NewWindowRequested.");
        validate(target); Target = target;
    }
    internal void Complete() => active = false;
}

/// <summary>Optional native popup support, preserving the original peer/settings constructors.</summary>
/// <remarks>Backend contract; all calls require the owning UI thread. None of these checks authenticates a page.</remarks>
public interface IWebViewNewWindowPeer : IWebViewPeer
{
    /// <summary>Gets whether current initialized native new-window and close hooks are installed.</summary>
    bool SupportsNewWindows { get; }
    /// <summary>Applies only the validated popup policy without changing scripts, user agent, navigation or layout.</summary>
    void ApplyNewWindowPolicy(WebViewNewWindowPolicy policy);
    /// <summary>Checks native freshness and environment/context compatibility without mutating either peer.</summary>
    bool CanAcceptNewWindowFrom(IWebViewNewWindowPeer opener);
    /// <summary>Prepares the canonical reserved request without initiating a separate native navigation.</summary>
    void PrepareNewWindow(WebViewNavigationRequest request);
    /// <summary>Aborts only an uncommitted handoff, preserving any newer request.</summary>
    void CancelNewWindow(long requestId);
}

/// <summary>Optional native-to-shared popup callback contract; older clients/adapters retain deny behavior.</summary>
public interface IWebViewNewWindowPeerClient
{
    /// <summary>Requests a scoped handoff; null means deny. Metadata is untrusted and Android lacks source identity.</summary>
    WebViewNewWindowTarget? NewWindowRequested(long requestId, Uri? destination, bool userInitiated);
    /// <summary>Reports an informational script-close intent; the application owns the actual control/window close.</summary>
    void CloseRequested(long requestId);
}

/// <summary>Backend-only handoff of an application-approved target using canonical request/lifetime state.</summary>
/// <remarks>Consume synchronously; recheck IsCurrent before native handoff. Never cache as page authority.</remarks>
public sealed class WebViewNewWindowTarget
{
    private readonly Func<bool> current;
    private readonly Action reject;
    internal WebViewNewWindowTarget(IWebViewNewWindowPeer peer, WebViewNavigationRequest request, Func<bool> current, Action reject)
    { Peer = peer; Request = request; this.current = current; this.reject = reject; }
    /// <summary>Gets the existing native peer owned by the target's NativeViewHost lease.</summary>
    public IWebViewNewWindowPeer Peer { get; }
    /// <summary>Gets the canonical reserved target request.</summary>
    public WebViewNavigationRequest Request { get; }
    /// <summary>Gets whether both controls, policies, request identities and cancellation tokens remain current.</summary>
    public bool IsCurrent => current();
    /// <summary>Releases an uncommitted native reservation and reports failure only if its request is still current.</summary>
    public void Reject() { Peer.CancelNewWindow(Request.Id); reject(); }
}
