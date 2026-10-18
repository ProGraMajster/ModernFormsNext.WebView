using System.ComponentModel;
using System.Text.Json;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView;

/// <summary>Embeds a browser through the framework's canonical native hosting boundary.</summary>
/// <remarks>
/// Experimental. All access that changes state requires the MFN UI thread. Native layout,
/// focus, validation, clipping, airspace and recreation belong to NativeViewHost. Dispose on
/// the UI thread. JavaScript is disabled by default; enable only under application policy.
/// </remarks>
/// <example><code>
/// // Register the platform provider at application startup first.
/// var browser = new WebView { Dock = DockStyle.Fill, Source = new Uri("https://example.com") };
/// form.Controls.Add(browser);
/// </code></example>
public class WebView : NativeViewHost
{
    private readonly IWebViewPeerFactoryProvider? explicitProvider;
    private Lease? lease;
    private WebViewNavigationRequest? requested;
    private WebViewNavigationRequest? committed;
    private Uri? source;
    private long generation, requestId;
    private long terminalRequestId = -1;
    private bool javaScriptEnabled, isLoading, canGoBack, canGoForward;
    private string? userAgent, platform, engine, engineVersion;
    private WebViewErrorCategory? lastFailure;
    private WebViewInitializationState initializationState;
    private CancellationTokenSource documentLifetime = new();

    /// <summary>Creates a safe placeholder, resolving the registered provider only when hosted.</summary>
    public WebView() { Text = "WebView"; PeerFactory = new Factory(this); }

    /// <summary>Creates a control with an explicitly owned provider, useful for scoped hosts and tests.</summary>
    /// <remarks>The control does not dispose the provider. No native engine is created by this constructor.</remarks>
    public WebView(IWebViewPeerFactoryProvider provider) : this()
        => explicitProvider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>Gets or requests the current URI; null requests about:blank and raw HTML reports null.</summary>
    /// <remarks>
    /// Before initialization only the latest request is retained. Same-URI assignment is a no-op;
    /// Navigate explicitly repeats a request. Redirect/completion update Source without navigating
    /// again. Cancellation restores the last committed request. Recreation replays URI/HTML but
    /// resets native history. Designer serialization uses SourceAddress because MFN does not edit Uri.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Uri? Source
    {
        get => source;
        set { VerifyAccess(); ValidateUri(value); if (source == value && requested?.Html is null) return; Navigate(value); }
    }

    /// <summary>Gets or sets the URI text for the Designer's safe primitive serialization path.</summary>
    /// <remarks>Empty or null text requests about:blank. This maps to Source; no browser starts in the Designer.</remarks>
    [DefaultValue("")]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string SourceAddress
    {
        get => source?.AbsoluteUri ?? "";
        set => Source = string.IsNullOrEmpty(value) ? null : new Uri(value, UriKind.Absolute);
    }

    /// <summary>Gets whether the active native browser has a backward history entry.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanGoBack => canGoBack;
    /// <summary>Gets whether the active native browser has a forward history entry.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanGoForward => canGoForward;
    /// <summary>Gets whether the current logical request is loading.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsLoading => isLoading;
    /// <summary>Gets whether the active browser is ready.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsInitialized => initializationState == WebViewInitializationState.Ready;
    /// <summary>Gets the initialization state independently of hosting diagnostics.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WebViewInitializationState InitializationState => initializationState;
    /// <summary>Gets the initialized baseline operation surface; None before engine readiness.</summary>
    /// <remarks>Scripting and messaging additionally require IsJavaScriptEnabled; cancellation is not a sandbox.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WebViewCapabilities BrowserCapabilities => IsInitialized ? WebViewCapabilities.UriNavigation |
        WebViewCapabilities.RawHtml | WebViewCapabilities.History | WebViewCapabilities.Scripting |
        WebViewCapabilities.Messaging | WebViewCapabilities.NavigationCancellation | WebViewCapabilities.UserAgent |
        (lease?.Peer is IWebViewLocalContentPeer { SupportsLocalContent: true } ? WebViewCapabilities.LocalContent : WebViewCapabilities.None) |
        (lease?.Peer is IWebViewLocalContentPolicyPeer { SupportsLocalContent: true, SupportsLocalContentPolicy: true }
            ? WebViewCapabilities.ConfigurableLocalContent : WebViewCapabilities.None) : WebViewCapabilities.None;

    /// <summary>Gets or enables JavaScript and the minimal string bridge; defaults to false.</summary>
    /// <remarks>UI thread only. Disabling cancels outstanding scripts/messages. Page code is untrusted.</remarks>
    [DefaultValue(false)]
    public bool IsJavaScriptEnabled
    {
        get => javaScriptEnabled;
        set
        {
            VerifyAccess(); if (javaScriptEnabled == value) return;
            javaScriptEnabled = value;
            if (!value) CancelDocumentOperations();
            if (IsInitialized) lease?.Peer.ApplySettings(Settings);
        }
    }

    /// <summary>Gets or sets the user agent override; null restores the engine default.</summary>
    /// <remarks>UI thread only. Changes apply to future requests, without reloading the page.</remarks>
    [DefaultValue(null)]
    public string? UserAgent
    {
        get => userAgent;
        set
        {
            VerifyAccess();
            if (value is not null && (value.Length == 0 || value.Any(char.IsControl)))
                throw new ArgumentException("UserAgent must be nonempty and contain no control characters.", nameof(value));
            if (userAgent == value) return;
            userAgent = value; if (IsInitialized) lease?.Peer.ApplySettings(Settings);
        }
    }

    /// <summary>Occurs before accepted main-frame navigation; observers may cancel or navigate reentrantly.</summary>
    public event EventHandler<WebViewNavigationStartingEventArgs>? NavigationStarting;
    /// <summary>Occurs after main-frame source/history updates; loading settles in a finally block.</summary>
    public event EventHandler<WebViewNavigationCompletedEventArgs>? NavigationCompleted;
    /// <summary>Occurs on a main-frame failure; subresource failures are excluded.</summary>
    public event EventHandler<WebViewNavigationFailedEventArgs>? NavigationFailed;
    /// <summary>Occurs after initialization state changes, including detachment and disposal.</summary>
    public event EventHandler? InitializationStateChanged;
    /// <summary>Occurs after an isolated browser initialization failure.</summary>
    public event EventHandler<WebViewInitializationFailedEventArgs>? InitializationFailed;
    /// <summary>Occurs after source, loading or history state changes, on the UI thread.</summary>
    public event EventHandler? StateChanged;
    /// <summary>Occurs for untrusted string data from the active document; Source is informational.</summary>
    public event EventHandler<WebViewMessageReceivedEventArgs>? WebMessageReceived;

    private WebViewSettings Settings => new(javaScriptEnabled, userAgent);
    private void VerifyAccess()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(Disposing || initializationState == WebViewInitializationState.Disposed, this);
    }

    /// <summary>Requests a URI, including a repeated URI; null requests about:blank.</summary>
    /// <remarks>UI thread only. Supports absolute HTTP/HTTPS and about:blank; other schemes are rejected.</remarks>
    public void Navigate(Uri? uri)
    {
        VerifyAccess(); ValidateUri(uri);
        Submit(new(++requestId, uri, null));
    }

    /// <summary>Requests raw HTML without a base URI; Source is null and relative network URLs have no base.</summary>
    /// <remarks>UI thread only. Browser history behavior is engine-specific. Recreation replays the HTML.</remarks>
    public void NavigateToHtml(string html)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(html);
        Submit(new(++requestId, null, html));
    }

    /// <summary>Loads explicitly public application resources at their isolated HTTPS origin.</summary>
    /// <remarks>
    /// UI thread only. No filesystem mapping or server is created. The catalog's immutable policy
    /// defaults to self external scripts/styles/images only; JavaScript still defaults off. Navigation/reload and
    /// canonical host recreation retain the immutable collection. Messages remain untrusted.
    /// Unsupported ready adapters throw NotSupportedException; queued requests fail with Engine
    /// on initialization if interception or the selected policy is unsupported. Nondefault policies require
    /// ConfigurableLocalContent; choosing one never authenticates page messages. Does not trigger framework layout.
    /// </remarks>
    public void NavigateToLocalContent(WebViewLocalContent content, string entryPath = "index.html")
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(content);
        Uri uri = content.GetUri(entryPath);
        if (IsInitialized && !CanLoadLocalContent(content))
            throw new NotSupportedException("This browser does not support the selected local-content policy.");
        Submit(new(++requestId, uri, null) { LocalContent = content });
    }

    private bool CanLoadLocalContent(WebViewLocalContent content) =>
        lease?.Peer is IWebViewLocalContentPeer { SupportsLocalContent: true } &&
        (content.Policy.IsDefault || lease.Peer is IWebViewLocalContentPolicyPeer { SupportsLocalContentPolicy: true });

    private void Submit(WebViewNavigationRequest request)
    {
        CancelDocumentOperations(); requested = request; source = request.Uri; isLoading = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (request.Id == requestId && IsInitialized) lease?.Peer.Navigate(request);
    }

    /// <summary>Goes back on the UI thread; returns false while loading or without an active history entry.</summary>
    public bool GoBack() { VerifyAccess(); if (!IsInitialized || isLoading || !canGoBack) return false; NewCommand(false); lease?.Peer.GoBack(requestId); return true; }
    /// <summary>Goes forward on the UI thread; returns false while loading or without an active history entry.</summary>
    public bool GoForward() { VerifyAccess(); if (!IsInitialized || isLoading || !canGoForward) return false; NewCommand(false); lease?.Peer.GoForward(requestId); return true; }
    /// <summary>Reloads on the UI thread; returns false while loading, detached or without a logical page.</summary>
    public bool Reload()
    {
        VerifyAccess(); if (!IsInitialized || isLoading || requested is null) return false;
        if (requested.LocalContent is { } local && !CanLoadLocalContent(local)) return false;
        NewCommand(true);
        // Reapply the collection after a canceled replacement may have revoked native
        // resource access. Reloading local content reissues the current URI explicitly.
        if (requested!.LocalContent is not null) lease?.Peer.Navigate(requested);
        else lease?.Peer.Reload(requestId);
        return true;
    }
    private void NewCommand(bool preserveHtml) { ++requestId; CancelDocumentOperations(); requested = new(requestId, source, preserveHtml ? requested?.Html : null) { LocalContent = requested?.LocalContent }; }

    /// <summary>Stops loading and cancels pending document operations; no-op when detached.</summary>
    /// <remarks>UI thread only. Late completion for the stopped request cannot update state.</remarks>
    public void Stop()
    {
        VerifyAccess(); ++requestId; CancelDocumentOperations();
        try { lease?.Peer.Stop(); }
        finally { isLoading = false; StateChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>Executes explicit JavaScript and returns one JSON encoded value; undefined becomes "null".</summary>
    /// <remarks>
    /// Call on the UI thread after initialization with JavaScript enabled. The task is canceled
    /// by navigation, Stop, peer retirement, disposal or its token. Native failures use
    /// WebViewOperationException. Runtime JavaScript exceptions follow native null-result behavior.
    /// JSON-compatible strings, numbers, booleans, null, objects and arrays use native JSON
    /// encoding. Undefined becomes JSON null. Cyclic or otherwise non-JSON values use the
    /// engine's projection: WebView2 can return null while Android may replace cyclic members
    /// with null. Return a JSON-compatible value when portable result semantics are required.
    /// Scripts execute natively; no eval wrapper or page-global helper changes their scope.
    /// Requested cancellation takes precedence over a coincident engine failure.
    /// </remarks>
    public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(script); EnsureScriptReady();
        return RunOperationAsync(token => lease!.Peer.ExecuteScriptAsync(script, token), cancellationToken);
    }

    /// <summary>Posts an untrusted string to the current page's window message listeners.</summary>
    /// <remarks>UI-thread invocation; same readiness, cancellation and lifetime contract as ExecuteScriptAsync.</remarks>
    public async Task PostMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(message); EnsureScriptReady();
        await RunOperationAsync(async token => { await lease!.Peer.PostMessageAsync(message, token); return "null"; }, cancellationToken);
    }

    private void EnsureScriptReady()
    {
        if (!IsInitialized) throw new InvalidOperationException("The browser is not initialized.");
        if (!javaScriptEnabled) throw new InvalidOperationException("Enable JavaScript explicitly before using scripts or messaging.");
    }

    private async Task<string> RunOperationAsync(Func<CancellationToken, Task<string>> operation, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, documentLifetime.Token);
        linked.Token.ThrowIfCancellationRequested();
        try
        {
            string result = await operation(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            using var json = JsonDocument.Parse(result);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (WebViewOperationException) { linked.Token.ThrowIfCancellationRequested(); throw; }
        catch { linked.Token.ThrowIfCancellationRequested(); throw new WebViewOperationException(WebViewErrorCategory.Engine); }
    }

    private void CancelDocumentOperations()
    {
        var previous = documentLifetime; documentLifetime = new();
        try { previous.Cancel(); } finally { previous.Dispose(); }
    }

    // Reuse the control's existing lease/request/token. Transport layers must not maintain
    // another document counter or infer readiness from Source or a native frame URL.
    internal WebViewDocumentContext GetTransportDocument()
    {
        VerifyAccess();
        return new(requestId, generation, documentLifetime.Token, IsInitialized && javaScriptEnabled &&
            !isLoading && terminalRequestId == requestId && lastFailure is null && requested is not null);
    }

    /// <summary>Returns a detached diagnostic snapshot; do not log Source without application redaction.</summary>
    public WebViewDiagnostics GetDiagnostics() => new(platform, engine, engineVersion, initializationState,
        source, isLoading, canGoBack, canGoForward, lastFailure, generation);

    /// <summary>Validates the baseline address policy without loading content.</summary>
    public static void ValidateUri(Uri? uri)
    {
        if (uri is null) return;
        if (!uri.IsAbsoluteUri || !(uri.Scheme is "https" or "http" || uri.AbsoluteUri == "about:blank"))
            throw new ArgumentException("Only absolute HTTP/HTTPS addresses and about:blank are supported.", nameof(uri));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        Dispatcher.UIThread.VerifyAccess();
        if (initializationState == WebViewInitializationState.Disposed) return;
        initializationState = WebViewInitializationState.Disposed; ++requestId;
        try { documentLifetime.Cancel(); }
        finally
        {
            try { base.Dispose(true); }
            finally { documentLifetime.Dispose(); requested = committed = null; isLoading = canGoBack = canGoForward = false; }
        }
    }

    private sealed class Factory(WebView owner) : INativeViewFactory
    {
        public INativeViewPeer CreatePeer(INativeViewSite site)
        {
            var client = new Client(owner, site, ++owner.generation);
            owner.initializationState = WebViewInitializationState.Initializing;
            owner.InitializationStateChanged?.Invoke(owner, EventArgs.Empty);
            IWebViewPeer peer;
            try
            {
                var provider = owner.explicitProvider ?? PlatformServiceRegistry.GetRequiredService<IWebViewPeerFactoryProvider>();
                peer = provider.CreatePeer(site, client);
            }
            catch
            {
                client.InitializationFailed(WebViewErrorCategory.Engine);
                throw;
            }
            var next = new Lease(owner, client, peer);
            owner.lease = next;
            try { peer.Initialize(owner.Settings); }
            catch { next.Dispose(); throw; }
            return next;
        }
    }

    private sealed class Lease(WebView owner, Client client, IWebViewPeer peer) : INativeViewPeer
    {
        public IWebViewPeer Peer { get; } = peer;
        private bool disposed;
        public void Resize(WindowKit.PixelSize size) { if (!disposed) Peer.Resize(size); }
        public void RequestFocus() { if (!disposed) Peer.RequestFocus(); }
        public bool TryMoveFocus(bool forward) => !disposed && Peer.TryMoveFocus(forward);
        public void Dispose()
        {
            if (disposed) return; disposed = true; client.Retired = true;
            bool active = ReferenceEquals(owner.lease, this);
            if (active)
            {
                owner.lease = null; ++owner.generation; ++owner.requestId;
                owner.isLoading = owner.canGoBack = owner.canGoForward = false;
                if (owner.initializationState != WebViewInitializationState.Disposed)
                    owner.initializationState = WebViewInitializationState.Detached;
            }
            try { if (active) owner.CancelDocumentOperations(); }
            finally
            {
                // Cancellation registrations are extension code and may throw. The native
                // peer must still release controllers/Views before propagating that failure.
                try { Peer.Dispose(); }
                finally
                {
                    if (active && owner.initializationState != WebViewInitializationState.Disposed)
                    { owner.InitializationStateChanged?.Invoke(owner, EventArgs.Empty); owner.StateChanged?.Invoke(owner, EventArgs.Empty); }
                }
            }
        }
    }

    private sealed class Client(WebView owner, INativeViewSite site, long identity) : IWebViewPeerClient
    {
        internal bool Retired;
        public bool IsCurrent => !Retired && site.IsCurrent && identity == owner.generation &&
            !owner.Disposing && owner.initializationState != WebViewInitializationState.Disposed;
        private bool Current(long id) => IsCurrent && id == owner.requestId;
        public void Initialized(string platform, string engine, string? version)
        {
            if (!IsCurrent) return;
            owner.platform = platform; owner.engine = engine; owner.engineVersion = version;
            owner.initializationState = WebViewInitializationState.Ready;
            long beforeNotification = owner.requestId;
            owner.InitializationStateChanged?.Invoke(owner, EventArgs.Empty);
            if (!IsCurrent) return;
            owner.lease?.Peer.ApplySettings(owner.Settings);
            if (owner.requestId == beforeNotification && owner.requested is { } previous)
            {
                var replay = previous with { Id = ++owner.requestId }; owner.requested = replay;
                if (replay.LocalContent is { } local && !owner.CanLoadLocalContent(local))
                    NavigationFailed(replay.Id, replay.Uri, WebViewErrorCategory.Engine);
                else owner.lease?.Peer.Navigate(replay);
            }
        }
        public void InitializationFailed(WebViewErrorCategory category)
        {
            if (!IsCurrent) return;
            owner.initializationState = WebViewInitializationState.Failed; owner.lastFailure = category;
            owner.isLoading = false;
            try { owner.InitializationStateChanged?.Invoke(owner, EventArgs.Empty); }
            finally { if (IsCurrent) owner.InitializationFailed?.Invoke(owner, new(category)); }
        }
        public long BeginNativeNavigation()
        {
            if (!IsCurrent) return 0;
            ++owner.requestId; owner.CancelDocumentOperations();
            owner.requested = new(owner.requestId, owner.source, null) { LocalContent = owner.requested?.LocalContent };
            return owner.requestId;
        }
        public bool NavigationStarting(long id, Uri? uri, bool isRedirect)
        {
            if (!Current(id)) return false;
            // Local documents may navigate only inside their collection. This is defense
            // in depth alongside response CSP, never a claim of complete network interception.
            if (owner.requested?.LocalContent is { } content &&
                (uri is null || !uri.IsAbsoluteUri || !uri.AbsoluteUri.StartsWith(content.Origin.AbsoluteUri, StringComparison.Ordinal)))
            { NavigationFailed(id, uri, WebViewErrorCategory.InvalidAddress); return false; }
            // Engine-private data/about addresses used for raw HTML, including Reload,
            // are represented by null in the logical API and never bypass URI policy
            // for a later user navigation (BeginNativeNavigation clears the HTML request).
            if (owner.requested?.Html is not null && !isRedirect &&
                (uri is null || uri.Scheme == "data" || uri.AbsoluteUri == "about:blank")) uri = null;
            try { ValidateUri(uri); }
            catch (ArgumentException) { NavigationFailed(id, uri, WebViewErrorCategory.InvalidAddress); return false; }
            var args = new WebViewNavigationStartingEventArgs(id, uri, isRedirect);
            try { owner.NavigationStarting?.Invoke(owner, args); }
            catch
            {
                if (Current(id)) { owner.isLoading = false; ++owner.requestId; }
                throw;
            }
            if (!Current(id)) return false;
            if (args.Cancel)
            {
                owner.source = owner.committed?.Uri; owner.requested = owner.committed;
                owner.isLoading = false; ++owner.requestId;
                owner.CancelDocumentOperations(); owner.StateChanged?.Invoke(owner, EventArgs.Empty);
                return false;
            }
            owner.source = owner.requested?.Html is null ? uri : null;
            owner.isLoading = true; owner.lastFailure = null;
            owner.StateChanged?.Invoke(owner, EventArgs.Empty);
            return Current(id);
        }
        public void NavigationCompleted(long id, Uri? uri, int? httpStatusCode)
        {
            if (!Current(id) || owner.terminalRequestId == id) return;
            owner.terminalRequestId = id;
            owner.source = owner.requested?.Html is null ? uri : null;
            owner.committed = owner.requested = new(id, owner.source, owner.requested?.Html) { LocalContent = owner.requested?.LocalContent };
            try { owner.NavigationCompleted?.Invoke(owner, new(id, owner.source, httpStatusCode)); }
            finally { Settle(id); }
        }
        public void NavigationFailed(long id, Uri? uri, WebViewErrorCategory category)
        {
            if (!Current(id) || owner.terminalRequestId == id) return;
            owner.terminalRequestId = id;
            owner.lastFailure = category;
            try { owner.NavigationFailed?.Invoke(owner, new(id, uri, category)); }
            finally { Settle(id); }
        }
        private void Settle(long id)
        {
            if (!Current(id)) return;
            owner.isLoading = false; owner.StateChanged?.Invoke(owner, EventArgs.Empty);
        }
        public void HistoryChanged(long id, bool back, bool forward)
        {
            if (!Current(id)) return;
            if (owner.canGoBack == back && owner.canGoForward == forward) return;
            owner.canGoBack = back; owner.canGoForward = forward;
            owner.StateChanged?.Invoke(owner, EventArgs.Empty);
        }
        public void WebMessageReceived(long id, string message, Uri? source)
        {
            if (Current(id) && owner.javaScriptEnabled) owner.WebMessageReceived?.Invoke(owner,
                new(message, source) { RequestId = id, PeerGeneration = identity });
        }
    }
}
