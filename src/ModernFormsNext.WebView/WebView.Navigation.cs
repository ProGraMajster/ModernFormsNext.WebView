using System.ComponentModel;

namespace ModernFormsNext.WebView;

public partial class WebView
{
    private WebViewNavigationSchemes allowedNavigationSchemes = WebViewNavigationSchemes.Default;

    /// <summary>Gets or sets this instance's permitted main-navigation URI schemes; defaults to Default.</summary>
    /// <remarks>
    /// UI thread only; hidden from unqualified Designer serialization. A changed value uses Stop's
    /// canonical cancellation/identity and stops current loading, without unloading the visible
    /// page, triggering layout or automatically reloading. Reload/new navigation establishes a
    /// new document context; a now-forbidden URI cannot reload/replay after native recreation.
    /// Equal assignment is a no-op. The value survives peer recreation and affects no other view.
    ///
    /// App URI commands are checked before native submission. Reported main-frame callbacks are
    /// filtered before NavigationStarting observers. Raw HTML/private initial engine URLs are
    /// separate, so None is not an offline mode. Frames/subresources and already-sent Android
    /// POST requests are not a network boundary. Local catalogs retain their origin/CSP rules.
    ///
    /// Nondefault settings require BrowserCapabilities.NavigationSchemeFiltering: Windows after
    /// native hook initialization; Android API24+ after installing its main-frame-aware client.
    /// Android API23/older adapters do not support this opt-in. Ready unsupported setters/loads
    /// throw NotSupportedException; queued unsupported loads fail with Engine and no submission.
    /// Detached configuration is retained for validation at initialization. Invalid flag bits
    /// throw ArgumentOutOfRangeException. Disallowed URI commands throw ArgumentException before
    /// canceling the current document; reported violations raise NavigationFailed(InvalidAddress).
    ///
    /// Explicit opt-in: Https | AboutBlank restricts main-navigation targets. Advanced higher risk:
    /// re-enabling Http allows cleartext where the OS permits it, without overriding TLS, Android
    /// manifest/mixed-content policy or authenticating page-to-C# messages. No unlisted scheme can
    /// be enabled. Other platforms must explicitly implement the optional native capability.
    /// </remarks>
    /// <example><code>
    /// // Configure before hosting; check the capability after initialization.
    /// browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https | WebViewNavigationSchemes.AboutBlank;
    /// browser.Navigate(new Uri("https://example.com/"));
    /// // Higher risk, only if the app deliberately accepts cleartext:
    /// browser.AllowedNavigationSchemes |= WebViewNavigationSchemes.Http;
    /// </code></example>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [DefaultValue(WebViewNavigationSchemes.Default)]
    public WebViewNavigationSchemes AllowedNavigationSchemes
    {
        get => allowedNavigationSchemes;
        set
        {
            VerifyAccess();
            if ((value & ~WebViewNavigationSchemes.Default) != 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Only baseline navigation scheme flags are supported.");
            if (value == allowedNavigationSchemes) return;
            if (IsInitialized && value != WebViewNavigationSchemes.Default && !HasNavigationSchemeFiltering)
                throw new NotSupportedException("The current native peer cannot filter reported main-frame navigation.");
            allowedNavigationSchemes = value;
            // Reuse the existing request/lifetime/bridge revocation. No second document counter.
            Stop();
        }
    }

    private bool HasNavigationSchemeFiltering =>
        lease?.Peer is IWebViewNavigationFilterPeer { SupportsNavigationSchemeFiltering: true };
    private bool CanApplyNavigationSchemes =>
        allowedNavigationSchemes == WebViewNavigationSchemes.Default || HasNavigationSchemeFiltering;

    private void EnsureNavigationSchemeSupport()
    {
        if (IsInitialized && !CanApplyNavigationSchemes)
            throw new NotSupportedException("The current native peer cannot apply the configured main-navigation filter.");
    }

    private bool IsNavigationUriAllowed(Uri? uri)
    {
        if (uri is not null && !uri.IsAbsoluteUri) return false;
        WebViewNavigationSchemes flag = uri is null || uri.AbsoluteUri == "about:blank"
            ? WebViewNavigationSchemes.AboutBlank
            : uri.Scheme switch { "https" => WebViewNavigationSchemes.Https,
                "http" => WebViewNavigationSchemes.Http, _ => WebViewNavigationSchemes.None };
        return flag != WebViewNavigationSchemes.None && (allowedNavigationSchemes & flag) != 0;
    }

    private void ValidateNavigationUri(Uri? uri)
    {
        ValidateUri(uri);
        if (!IsNavigationUriAllowed(uri))
            throw new ArgumentException("The URI scheme is not allowed by this WebView's navigation policy.", nameof(uri));
    }
}
