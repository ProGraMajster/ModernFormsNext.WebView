using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView;

/// <summary>A snapshot of the canonical control's document lifetime; no origin authentication is implied.</summary>
/// <param name="RequestId">Original logical document request identity.</param>
/// <param name="PeerGeneration">Current native peer generation.</param>
/// <param name="CancellationToken">Canceled by navigation, Stop, JS disable, retirement or control disposal.</param>
/// <param name="IsReady">Whether this logical document completed successfully with JavaScript enabled.</param>
/// <remarks>Snapshots may be inspected off-thread. Obtain them on the owning UI thread; IDs are not secrets.</remarks>
public readonly record struct WebViewDocumentContext(long RequestId, long PeerGeneration, CancellationToken CancellationToken, bool IsReady);

/// <summary>An untrusted string forwarded with its original canonical document context.</summary>
/// <param name="message">Untrusted page data.</param>
/// <param name="source">Informational source, never an authorization claim.</param>
/// <param name="document">The matching document snapshot captured before observer delivery.</param>
public sealed class WebViewTransportMessageEventArgs(string message, Uri? source, WebViewDocumentContext document) : EventArgs
{
    /// <summary>Gets the untrusted string.</summary>
    public string Message { get; } = message;
    /// <summary>Gets the informational source, or null.</summary>
    public Uri? Source { get; } = source;
    /// <summary>Gets the original document lifetime.</summary>
    public WebViewDocumentContext Document { get; } = document;
    /// <summary>Gets whether this baseline transport authenticates the original caller; always false.</summary>
    public bool IsOriginAuthenticated => false;
}

/// <summary>Reusable script/string transport over a canonical browser document.</summary>
/// <remarks>
/// Invoke operations, subscribe and dispose on the owning UI thread. Async completion can occur
/// elsewhere. Implementations borrow their browser. Documents and LifetimeToken revoke pending
/// work independently; cancellation cannot undo submitted JavaScript effects. No privileged
/// message authorization or automatic script installation is provided.
/// </remarks>
public interface IWebViewTransport : IDisposable
{
    /// <summary>Gets the current canonical document snapshot on the UI thread.</summary>
    WebViewDocumentContext CurrentDocument { get; }
    /// <summary>Gets a token canceled when this transport is disposed; remains readable afterwards.</summary>
    CancellationToken LifetimeToken { get; }
    /// <summary>Occurs on the UI thread for an untrusted string in its original current document.</summary>
    event EventHandler<WebViewTransportMessageEventArgs>? MessageReceived;
    /// <summary>Executes explicit native JavaScript and returns its raw JSON result.</summary>
    Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default);
    /// <summary>Submits an escaped untrusted string; completion is submission, not delivery.</summary>
    Task PostMessageAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>Adapts an existing WebView to a reusable, independently disposable transport.</summary>
/// <remarks>
/// Owns only subscriptions and its cancellation source. The borrowed WebView retains all native
/// hosting, readiness, document identity, validation and dispatcher behavior. Dispose on the UI
/// thread; this never disposes or navigates the browser. No second lifecycle counter is created.
/// </remarks>
/// <example><code>
/// using var transport = new WebViewTransport(browser);
/// transport.MessageReceived += (_, e) => ValidateUntrustedData(e.Message);
/// await transport.PostMessageAsync("hello", cancellationToken);
/// </code></example>
public sealed class WebViewTransport : IWebViewTransport
{
    private readonly WebView browser;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;

    /// <summary>Creates a transport borrowing the browser on its owning UI thread.</summary>
    public WebViewTransport(WebView browser)
    {
        Dispatcher.UIThread.VerifyAccess();
        this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _ = browser.GetTransportDocument();
        LifetimeToken = lifetime.Token;
        browser.WebMessageReceived += ForwardMessage;
    }

    /// <inheritdoc/>
    public WebViewDocumentContext CurrentDocument { get { VerifyAccess(); return browser.GetTransportDocument(); } }
    /// <inheritdoc/>
    public CancellationToken LifetimeToken { get; }
    /// <inheritdoc/>
    public event EventHandler<WebViewTransportMessageEventArgs>? MessageReceived;

    /// <inheritdoc/>
    public Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(script);
        return ExecuteAsync(script, cancellationToken);
    }
    private async Task<string> ExecuteAsync(string script, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, LifetimeToken);
        linked.Token.ThrowIfCancellationRequested();
        return await browser.ExecuteScriptAsync(script, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task PostMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(message);
        return PostAsync(message, cancellationToken);
    }
    private async Task PostAsync(string message, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, LifetimeToken);
        linked.Token.ThrowIfCancellationRequested();
        await browser.PostMessageAsync(message, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
    }

    private void ForwardMessage(object? sender, WebViewMessageReceivedEventArgs e)
    {
        if (disposed || browser.InitializationState == WebViewInitializationState.Disposed) return;
        var document = browser.GetTransportDocument();
        // A preceding baseline observer may navigate/dispose before this subscriber runs.
        // Keep original metadata; never relabel its message with the replacement document.
        if (e.RequestId != document.RequestId || e.PeerGeneration != document.PeerGeneration || document.CancellationToken.IsCancellationRequested) return;
        MessageReceived?.Invoke(this, new(e.Message, e.Source, document));
    }

    private void VerifyAccess()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    /// <summary>Unsubscribes and cancels this transport's tasks without releasing the borrowed browser.</summary>
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (disposed) return;
        disposed = true; browser.WebMessageReceived -= ForwardMessage; MessageReceived = null;
        try { lifetime.Cancel(); } finally { lifetime.Dispose(); }
    }
}
