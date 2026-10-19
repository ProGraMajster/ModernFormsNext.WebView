using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext.WebView;

/// <summary>Reports baseline browser operations available on an initialized lease.</summary>
/// <remarks>These flags describe operations, not authenticated origins, a sandbox or preserved history.</remarks>
[Flags]
public enum WebViewCapabilities
{
    /// <summary>No initialized browser operations are available.</summary>
    None = 0,
    /// <summary>Absolute HTTP/HTTPS and about:blank navigation.</summary>
    UriNavigation = 1,
    /// <summary>Raw HTML with no base URI.</summary>
    RawHtml = 2,
    /// <summary>Native back/forward history and reload/stop.</summary>
    History = 4,
    /// <summary>Explicit JSON-result scripting when JavaScript is enabled.</summary>
    Scripting = 8,
    /// <summary>Minimal untrusted string messaging when JavaScript is enabled.</summary>
    Messaging = 16,
    /// <summary>Cancellation of reported main-frame Starting callbacks, subject to platform hook limitations.</summary>
    NavigationCancellation = 32,
    /// <summary>A validated user agent override.</summary>
    UserAgent = 64,
    /// <summary>Immutable public application resources at a controlled HTTPS origin; no privileged messaging.</summary>
    LocalContent = 128,
    /// <summary>Reserved for separately qualified origin/frame-authenticated messaging.</summary>
    /// <remarks>Neither current adapter advertises this flag. Source and request IDs never enable it.</remarks>
    AuthenticatedMessaging = 256,
    /// <summary>Typed nondefault local-content CSP headers through available native interception; no native privileges.</summary>
    /// <remarks>Provider/runtime CSP effects require separate qualification. Unsupported policies fail explicitly.</remarks>
    ConfigurableLocalContent = 512,
    /// <summary>Per-view URI-scheme checks and cancellation of reported main-frame callbacks.</summary>
    /// <remarks>Not a network/frame firewall. Android requires API24+; POST fallback is after network start.</remarks>
    NavigationSchemeFiltering = 1024
}

/// <summary>Describes browser initialization independently of native hosting availability.</summary>
public enum WebViewInitializationState
{
    /// <summary>No current browser lease exists.</summary>
    Detached,
    /// <summary>The current lease is initializing its browser engine.</summary>
    Initializing,
    /// <summary>The current browser accepts navigation and script commands.</summary>
    Ready,
    /// <summary>Initialization failed; presentation replacement permits another attempt.</summary>
    Failed,
    /// <summary>The shared control has been disposed.</summary>
    Disposed
}

/// <summary>Stable failure categories without platform exceptions or error enums.</summary>
public enum WebViewErrorCategory
{
    /// <summary>The engine could not classify the failure.</summary>
    Unknown,
    /// <summary>A general network failure occurred.</summary>
    Network,
    /// <summary>TLS certificate validation failed.</summary>
    Certificate,
    /// <summary>The remote host could not be resolved.</summary>
    HostNotFound,
    /// <summary>A connection failed or timed out.</summary>
    Connection,
    /// <summary>The navigation was canceled.</summary>
    Canceled,
    /// <summary>The address is invalid or disallowed by baseline/per-view navigation policy.</summary>
    InvalidAddress,
    /// <summary>The browser engine could not perform the operation.</summary>
    Engine
}

/// <summary>Reports a neutral asynchronous browser operation failure.</summary>
public sealed class WebViewOperationException : Exception
{
    /// <summary>Creates a failure with a safe, fixed message and no native inner exception.</summary>
    public WebViewOperationException(WebViewErrorCategory category)
        : base($"WebView operation failed ({category}).") => Category = category;
    /// <summary>Gets the stable failure category.</summary>
    public WebViewErrorCategory Category { get; }
}

/// <summary>Describes one logical URI or raw HTML request owned by the shared control.</summary>
/// <param name="Id">Monotonically increasing identity, local to this control.</param>
/// <param name="Uri">Requested address; null for raw HTML.</param>
/// <param name="Html">Raw HTML; null for URI requests.</param>
public sealed record WebViewNavigationRequest(long Id, Uri? Uri, string? Html)
{
    /// <summary>Gets the optional immutable collection for this local URI request, retained for replay.</summary>
    /// <remarks>Only adapters advertising IWebViewLocalContentPeer may receive a non-null value.</remarks>
    public WebViewLocalContent? LocalContent { get; init; }
}

/// <summary>Immutable browser settings applied on the UI thread.</summary>
/// <param name="IsJavaScriptEnabled">Whether explicit scripts and page messaging are allowed.</param>
/// <param name="UserAgent">An override, or null for the engine default.</param>
public sealed record WebViewSettings(bool IsJavaScriptEnabled, string? UserAgent);

/// <summary>Detached engine information without native handles, profiles, content or messages.</summary>
/// <param name="Platform">Platform name.</param>
/// <param name="Engine">Browser engine name.</param>
/// <param name="EngineVersion">Engine/provider version when available.</param>
/// <param name="InitializationState">Current initialization state.</param>
/// <param name="CurrentSource">Informational requested/current URI; may contain application data.</param>
/// <param name="IsLoading">Whether the active logical navigation is loading.</param>
/// <param name="CanGoBack">Current native history availability.</param>
/// <param name="CanGoForward">Current native history availability.</param>
/// <param name="LastFailure">Last neutral failure category.</param>
/// <param name="PeerGeneration">Control-local lease generation.</param>
public sealed record WebViewDiagnostics(string? Platform, string? Engine, string? EngineVersion,
    WebViewInitializationState InitializationState, Uri? CurrentSource, bool IsLoading,
    bool CanGoBack, bool CanGoForward, WebViewErrorCategory? LastFailure, long PeerGeneration);

/// <summary>Supplies fresh browser leases through the existing PlatformServiceRegistry.</summary>
/// <remarks>Register explicitly at application startup. Implementations own app-level engine configuration.</remarks>
public interface IWebViewPeerFactoryProvider
{
    /// <summary>Creates a fresh, uninitialized lease against the current native site.</summary>
    /// <remarks>Ownership transfers to NativeViewHost; initialization is started after creation.</remarks>
    IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client);
}

/// <summary>Optional detection of installed native main-frame navigation filtering hooks.</summary>
/// <remarks>
/// Does not change IWebViewPeer or its settings/request constructors. Older adapters remain
/// compatible with Default navigation schemes; nondefault policies require this capability.
/// The shared control validates app URI commands and reported callbacks. Implementations must
/// report true only for an active initialized peer with installed, main-frame-aware hooks.
/// Windows uses Core NavigationStarting; Android requires API24+ request metadata and retains
/// the later OnPageStarted fallback for POST. This does not promise complete redirects, frames,
/// subresource interception, pre-network cancellation or authenticated message origin.
/// </remarks>
public interface IWebViewNavigationFilterPeer
{
    /// <summary>Gets whether current native hooks support the documented reported-main-frame filter.</summary>
    /// <remarks>UI thread only. False after retirement or on unsupported platform/API; never inferred from configuration intent.</remarks>
    bool SupportsNavigationSchemeFiltering { get; }
}

/// <summary>Feature operations added to the framework's owned native peer lease.</summary>
/// <remarks>All entry points require the owning UI thread. Implementations never position the host container.</remarks>
public interface IWebViewPeer : INativeViewPeer
{
    /// <summary>Starts feature initialization, potentially asynchronously, exactly once.</summary>
    void Initialize(WebViewSettings settings);
    /// <summary>Applies validated settings to the current browser.</summary>
    void ApplySettings(WebViewSettings settings);
    /// <summary>Submits the latest logical request, reporting Starting before native navigation.</summary>
    void Navigate(WebViewNavigationRequest request);
    /// <summary>Traverses backward with a new logical request identity.</summary>
    void GoBack(long requestId);
    /// <summary>Traverses forward with a new logical request identity.</summary>
    void GoForward(long requestId);
    /// <summary>Reloads with a new logical request identity.</summary>
    void Reload(long requestId);
    /// <summary>Stops current loading. Obsolete callbacks must retain their original identities.</summary>
    void Stop();
    /// <summary>Returns a JSON encoded JavaScript value; undefined maps to JSON null.</summary>
    /// <remarks>Execute natively. Cyclic and other non-JSON results follow the engine's projection;
    /// only JSON-compatible values have portable value semantics. No eval wrapper is implied.</remarks>
    Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken);
    /// <summary>Posts a string message using a safe, escaped transport.</summary>
    Task PostMessageAsync(string message, CancellationToken cancellationToken);
}

/// <summary>UI-thread callback boundary for browser adapters.</summary>
/// <remarks>Adapters consume this client, retaining original request identities. Stale callbacks are rejected.</remarks>
public interface IWebViewPeerClient
{
    /// <summary>Gets whether this lease and its native site remain current.</summary>
    bool IsCurrent { get; }
    /// <summary>Reports successful initialization; the control replays its latest request.</summary>
    void Initialized(string platform, string engine, string? version);
    /// <summary>Reports an isolated initialization failure with no native exception object.</summary>
    void InitializationFailed(WebViewErrorCategory category);
    /// <summary>Asks permission to start a main-frame request or redirect; false means cancel.</summary>
    bool NavigationStarting(long requestId, Uri? uri, bool isRedirect);
    /// <summary>Allocates an identity for native user/page/history navigation.</summary>
    long BeginNativeNavigation();
    /// <summary>Reports main-frame completion for the original request identity.</summary>
    void NavigationCompleted(long requestId, Uri? uri, int? httpStatusCode);
    /// <summary>Reports main-frame failure for the original request identity.</summary>
    void NavigationFailed(long requestId, Uri? uri, WebViewErrorCategory category);
    /// <summary>Reports history for the active request; stale updates are ignored.</summary>
    void HistoryChanged(long requestId, bool canGoBack, bool canGoForward);
    /// <summary>Reports a string message for the active document request.</summary>
    /// <param name="requestId">Original document request identity.</param>
    /// <param name="message">Untrusted page-provided string.</param>
    /// <param name="source">Informational document URI; null when the platform cannot identify the sender.</param>
    void WebMessageReceived(long requestId, string message, Uri? source);
}
