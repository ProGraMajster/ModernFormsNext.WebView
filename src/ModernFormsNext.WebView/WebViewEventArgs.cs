namespace ModernFormsNext.WebView;

/// <summary>Cancelable main-frame navigation notification, subject to platform interception limits.</summary>
/// <remarks>Android POST fallback notification may occur after network loading started; this event is not a sandbox.</remarks>
public sealed class WebViewNavigationStartingEventArgs(long requestId, Uri? uri, bool isRedirect) : EventArgs
{
    /// <summary>Gets the logical request identity.</summary>
    public long RequestId { get; } = requestId;
    /// <summary>Gets the destination, or null for raw HTML.</summary>
    public Uri? Uri { get; } = uri;
    /// <summary>Gets whether the platform adapter classifies this callback as a redirect during an ongoing load.</summary>
    /// <remarks>Windows uses the native flag; Android uses available load callbacks. This is not a user/programmatic intent flag.</remarks>
    public bool IsRedirect { get; } = isRedirect;
    /// <summary>Gets or sets whether to cancel the navigation. Cancellation is not a sandbox.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Reports a successfully completed main-frame request.</summary>
public sealed class WebViewNavigationCompletedEventArgs(long requestId, Uri? uri, int? httpStatusCode) : EventArgs
{
    /// <summary>Gets the completed logical request identity.</summary>
    public long RequestId { get; } = requestId;
    /// <summary>Gets the final URI, or null for raw HTML.</summary>
    public Uri? Uri { get; } = uri;
    /// <summary>Gets HTTP status when supplied by the platform; HTTP errors are not transport failures.</summary>
    public int? HttpStatusCode { get; } = httpStatusCode;
}

/// <summary>Reports a failed main-frame request without exposing native exceptions.</summary>
public sealed class WebViewNavigationFailedEventArgs(long requestId, Uri? uri, WebViewErrorCategory category) : EventArgs
{
    /// <summary>Gets the failed logical request identity.</summary>
    public long RequestId { get; } = requestId;
    /// <summary>Gets the failed destination when known.</summary>
    public Uri? Uri { get; } = uri;
    /// <summary>Gets the neutral failure category.</summary>
    public WebViewErrorCategory Category { get; } = category;
}

/// <summary>Reports an isolated browser initialization failure.</summary>
public sealed class WebViewInitializationFailedEventArgs(WebViewErrorCategory category) : EventArgs
{
    /// <summary>Gets the failure category; profile paths and native exceptions are never included.</summary>
    public WebViewErrorCategory Category { get; } = category;
}

/// <summary>Contains untrusted page data; applications must validate it before acting.</summary>
public sealed class WebViewMessageReceivedEventArgs(string message, Uri? source) : EventArgs
{
    /// <summary>Gets the untrusted string payload.</summary>
    public string Message { get; } = message;
    /// <summary>Gets an informational document URI, or null. This is not an authenticated origin.</summary>
    public Uri? Source { get; } = source;
}
