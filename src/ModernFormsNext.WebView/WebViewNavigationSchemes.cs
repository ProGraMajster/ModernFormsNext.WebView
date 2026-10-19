namespace ModernFormsNext.WebView;

/// <summary>Selects URI schemes accepted by one WebView's main-navigation filter.</summary>
/// <remarks>
/// This is a subset of the baseline HTTP/HTTPS/exact about:blank policy, never an arbitrary
/// protocol registration or network firewall. Raw HTML is a separate explicit operation;
/// subresources, frame loads and Android POST network effects are outside this filter.
/// No selection authenticates page code, changes CSP/TLS or grants native privileges.
/// </remarks>
[Flags]
public enum WebViewNavigationSchemes
{
    /// <summary>Rejects URI navigation, including null/blank; explicit raw HTML remains available.</summary>
    None = 0,
    /// <summary>Permits HTTP main-navigation targets, subject to OS cleartext policy.</summary>
    /// <remarks>Cleartext has higher confidentiality risk. This flag never overrides Android's manifest or mixed-content rules.</remarks>
    Http = 1,
    /// <summary>Permits HTTPS main-navigation targets with the engine's normal certificate validation.</summary>
    /// <remarks>HTTPS transport does not make a page or message an authorized native caller.</remarks>
    Https = 2,
    /// <summary>Permits exactly about:blank, including null Source/Navigate requests.</summary>
    AboutBlank = 4,
    /// <summary>Preserves the original HTTP, HTTPS and exact about:blank baseline behavior.</summary>
    /// <remarks>This is the default. Existing file/content/data/javascript/custom URI restrictions remain.</remarks>
    Default = Http | Https | AboutBlank
}
