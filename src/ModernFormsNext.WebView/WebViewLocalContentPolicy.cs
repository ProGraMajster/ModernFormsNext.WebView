using System.Collections.ObjectModel;

namespace ModernFormsNext.WebView;

/// <summary>Selects the permitted source list for one supported local-content CSP directive.</summary>
/// <remarks>This is not a flag enum. A source list does not authenticate scripts or grant native privileges.</remarks>
public enum WebViewLocalContentSource
{
    /// <summary>Denies the resource or operation using the CSP 'none' source expression.</summary>
    None,
    /// <summary>Permits the document's own origin using CSP 'self'; paths are still allowlisted by the catalog.</summary>
    Self
}

/// <summary>An immutable, experimental CSP policy for one public local-resource catalog.</summary>
/// <remarks>
/// Safe to share between threads and catalogs. Select before navigation; changing policy requires a
/// new catalog/document, using the existing WebView cancellation/lifetime rules. This configures
/// content loading only. It never enables JavaScript, native handlers, authenticated messaging or DI.
/// CSP denies frame/worker/object resource loads, forms, inline scripts/styles and page eval.
/// Inherited srcdoc/about:blank DOM contexts are not a complete frame/realm sandbox.
/// Native application ExecuteScript remains a separate explicit engine operation.
/// </remarks>
/// <example><code>
/// var content = new WebViewLocalContent(publicResources, WebViewLocalContentPolicy.Application);
/// browser.IsJavaScriptEnabled = true;
/// browser.NavigateToLocalContent(content);
/// // To deny page scripts instead:
/// var policy = new WebViewLocalContentPolicy(scripts: WebViewLocalContentSource.None);
/// </code></example>
public sealed class WebViewLocalContentPolicy
{
    /// <summary>Gets the original restrictive policy: self scripts/styles/images, no connections or base element.</summary>
    public static WebViewLocalContentPolicy Default { get; } = new();

    /// <summary>Gets an explicit application-resource policy permitting self connections and a self base URI.</summary>
    /// <remarks>
    /// Only those two directives differ from Default. Supply reviewed, public app-owned resources.
    /// This permits relative-URL resolution through a same-origin base and reading allowlisted bytes
    /// with fetch/XHR or other connections permitted by the engine's CSP self semantics. It is not a
    /// network firewall, verified whole-app realm or permission to dispatch JavaScript calls to C#.
    /// </remarks>
    public static WebViewLocalContentPolicy Application { get; } = new(
        connections: WebViewLocalContentSource.Self, baseUri: WebViewLocalContentSource.Self);

    /// <summary>Creates a typed policy; omitted selectors preserve the original default CSP.</summary>
    /// <param name="scripts">Source list for page scripts; Self permits external catalog scripts, never inline/eval.</param>
    /// <param name="styles">Source list for page stylesheets; inline styling is not enabled.</param>
    /// <param name="images">Source list for catalog images; data/remote sources and arbitrary MIME remain unavailable.</param>
    /// <param name="connections">Source list for browser connections; Self is deliberate opt-in to same-origin access.</param>
    /// <param name="baseUri">Source list for the HTML base element; Self permits any path on the same origin.</param>
    /// <exception cref="ArgumentOutOfRangeException">A selector is neither None nor Self.</exception>
    public WebViewLocalContentPolicy(
        WebViewLocalContentSource scripts = WebViewLocalContentSource.Self,
        WebViewLocalContentSource styles = WebViewLocalContentSource.Self,
        WebViewLocalContentSource images = WebViewLocalContentSource.Self,
        WebViewLocalContentSource connections = WebViewLocalContentSource.None,
        WebViewLocalContentSource baseUri = WebViewLocalContentSource.None)
    {
        Scripts = Validate(scripts, nameof(scripts)); Styles = Validate(styles, nameof(styles));
        Images = Validate(images, nameof(images)); Connections = Validate(connections, nameof(connections));
        BaseUri = Validate(baseUri, nameof(baseUri));
        // Only validated enum values become fixed CSP tokens. Never interpolate app-supplied
        // text into headers or let a mutable options object change an intercepted document.
        ContentSecurityPolicy = "default-src 'none'; script-src " + Token(Scripts) +
            "; style-src " + Token(Styles) + "; img-src " + Token(Images) +
            "; connect-src " + Token(Connections) + "; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri " +
            Token(BaseUri) + "; form-action 'none'; frame-ancestors 'none'";
        ResponseHeaders = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["Content-Security-Policy"] = ContentSecurityPolicy,
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["Cross-Origin-Resource-Policy"] = "same-origin",
            ["Cache-Control"] = "no-store",
            ["Referrer-Policy"] = "no-referrer"
        });
    }

    /// <summary>Gets the script-src selector; it does not enable the WebView JavaScript setting.</summary>
    public WebViewLocalContentSource Scripts { get; }
    /// <summary>Gets the style-src selector; inline styling remains unavailable.</summary>
    public WebViewLocalContentSource Styles { get; }
    /// <summary>Gets the img-src selector for allowlisted public image bytes.</summary>
    public WebViewLocalContentSource Images { get; }
    /// <summary>Gets the connect-src selector; it grants no native endpoint or CORS permission.</summary>
    public WebViewLocalContentSource Connections { get; }
    /// <summary>Gets the base-uri selector; Self permits same-origin base paths, not a single fixed path.</summary>
    public WebViewLocalContentSource BaseUri { get; }
    /// <summary>Gets the generated CSP header for diagnostics; applications cannot replace its text.</summary>
    public string ContentSecurityPolicy { get; }

    internal IReadOnlyDictionary<string, string> ResponseHeaders { get; }
    internal bool IsDefault => Scripts == WebViewLocalContentSource.Self && Styles == WebViewLocalContentSource.Self &&
        Images == WebViewLocalContentSource.Self && Connections == WebViewLocalContentSource.None && BaseUri == WebViewLocalContentSource.None;
    private static string Token(WebViewLocalContentSource source) => source == WebViewLocalContentSource.Self ? "'self'" : "'none'";
    private static WebViewLocalContentSource Validate(WebViewLocalContentSource source, string parameterName) =>
        source is WebViewLocalContentSource.None or WebViewLocalContentSource.Self ? source :
            throw new ArgumentOutOfRangeException(parameterName, source, "Use None or Self for a local-content CSP selector.");
}

/// <summary>Optional adapter support for nondefault typed local-content policy headers.</summary>
/// <remarks>
/// Older local-resource adapters need not implement this interface and keep supporting the default policy.
/// Declared support means validated catalog headers are delivered through available native interception;
/// it does not attest universal CSP enforcement, network sandboxing or original-script authentication.
/// </remarks>
public interface IWebViewLocalContentPolicyPeer : IWebViewLocalContentPeer
{
    /// <summary>Gets whether this current peer can deliver nondefault typed policy headers without substitution.</summary>
    bool SupportsLocalContentPolicy { get; }
}
