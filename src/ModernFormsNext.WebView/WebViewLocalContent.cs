using System.Collections.ObjectModel;

namespace ModernFormsNext.WebView;

/// <summary>An immutable, explicitly public collection of application web resources.</summary>
/// <remarks>
/// Experimental. Construction copies at most 16 MiB, with at most 1024 resources of 4 MiB each.
/// Paths are case-sensitive ASCII relative paths; URL escapes, dot segments and filesystem paths
/// are rejected. No directory, stream provider or private application resource is exposed.
/// This object can be read from any thread after construction. It owns no disposable resources.
/// Local content does not authenticate messages or grant application privileges.
/// </remarks>
/// <example><code>
/// var content = new WebViewLocalContent(new Dictionary&lt;string, byte[]&gt;
/// {
///     ["index.html"] = System.Text.Encoding.UTF8.GetBytes("&lt;link rel='stylesheet' href='app.css'&gt;&lt;h1&gt;Offline&lt;/h1&gt;"),
///     ["app.css"] = System.Text.Encoding.UTF8.GetBytes("h1 { color: navy; }")
/// });
/// browser.NavigateToLocalContent(content);
/// </code></example>
public sealed class WebViewLocalContent
{
    private const string HostSuffix = ".mfn-app.invalid";
    private readonly Dictionary<string, (byte[] Data, string Mime)> resources = new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> responseHeaders = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>
        {
            ["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; " +
                "connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; " +
                "form-action 'none'; frame-ancestors 'none'",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["Cross-Origin-Resource-Policy"] = "same-origin",
            ["Cache-Control"] = "no-store",
            ["Referrer-Policy"] = "no-referrer"
        });

    /// <summary>Copies the allowlisted public resources, inferring MIME from supported extensions.</summary>
    /// <exception cref="ArgumentException">A path, extension, count or size is unsupported.</exception>
    /// <remarks>HTML/CSS/JS/JSON/TXT are UTF-8; PNG/JPEG/GIF/WebP/ICO are binary. SVG is excluded.</remarks>
    public WebViewLocalContent(IReadOnlyDictionary<string, byte[]> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (resources.Count is 0 or > 1024) throw new ArgumentException("Supply 1 to 1024 public resources.", nameof(resources));
        int total = 0;
        foreach (var (path, bytes) in resources)
        {
            ValidatePath(path);
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length > 4 * 1024 * 1024 || (total += bytes.Length) > 16 * 1024 * 1024)
                throw new ArgumentException("Local content size limit exceeded.", nameof(resources));
            string mime = System.IO.Path.GetExtension(path) switch
            {
                ".html" => "text/html", ".css" => "text/css", ".js" => "text/javascript",
                ".json" => "application/json", ".txt" => "text/plain", ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".ico" => "image/x-icon",
                _ => throw new ArgumentException("Unsupported local resource extension.", nameof(resources))
            };
            this.resources.Add(path, (bytes.ToArray(), mime));
        }
    }

    /// <summary>Gets this collection's unique HTTPS origin, reserved for intercepted application resources.</summary>
    /// <remarks>The random host isolates collections; it is neither a secret nor sender authentication.</remarks>
    public Uri Origin { get; } = new("https://" + Guid.NewGuid().ToString("N") + HostSuffix + "/");

    /// <summary>Returns an entry URI for an existing, validated relative resource path.</summary>
    /// <exception cref="ArgumentException">The path is invalid or absent from the collection.</exception>
    public Uri GetUri(string path)
    {
        ValidatePath(path);
        if (!resources.ContainsKey(path)) throw new ArgumentException("The resource is not in this collection.", nameof(path));
        return new Uri(Origin.AbsoluteUri + path);
    }

    /// <summary>Identifies the reserved resource namespace, including inactive collection origins.</summary>
    /// <remarks>Adapters must intercept this namespace even without active content and return an error locally.</remarks>
    public static bool IsLocalUri(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
        (uri.Host.TrimEnd('.').EndsWith(HostSuffix, StringComparison.OrdinalIgnoreCase) ||
         uri.Host.TrimEnd('.').Equals("mfn-app.invalid", StringComparison.OrdinalIgnoreCase));

    /// <summary>Resolves a native request without I/O, redirects, URL canonicalization or application callbacks.</summary>
    /// <remarks>
    /// Safe on interception threads. Only exact-origin GET/HEAD requests are served. Query strings,
    /// escapes and noncanonical paths fail closed; browsers can normalize paths before this API sees them.
    /// Native adapters must never fall back to the network for a reserved namespace request.
    /// </remarks>
    public WebViewLocalContentResponse CreateResponse(string address, string method = "GET")
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.StartsWith(Origin.AbsoluteUri, StringComparison.Ordinal)) return ErrorResponse(404);
        string path = address[Origin.AbsoluteUri.Length..];
        if (!IsValidPath(path)) return ErrorResponse(400);
        if (method is not ("GET" or "HEAD")) return ErrorResponse(405);
        return resources.TryGetValue(path, out var resource)
            ? new(200, resource.Mime, method == "HEAD" ? [] : resource.Data, responseHeaders)
            : ErrorResponse(404);
    }

    /// <summary>Creates an empty denial response for an inactive or invalid reserved resource request.</summary>
    public static WebViewLocalContentResponse ErrorResponse(int statusCode = 410)
    {
        if (statusCode is not (400 or 404 or 405 or 410)) throw new ArgumentOutOfRangeException(nameof(statusCode));
        return new(statusCode, "text/plain", [], responseHeaders);
    }

    private static void ValidatePath(string path)
    {
        if (!IsValidPath(path)) throw new ArgumentException("Use a canonical ASCII relative resource path.", nameof(path));
    }

    private static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 1024) return false;
        foreach (char c in path)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/')) return false;
        foreach (string segment in path.Split('/'))
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.')) return false;
        return true;
    }
}

/// <summary>An immutable local resource response with a fresh, caller-owned read stream per request.</summary>
/// <remarks>No raw backing array is exposed. Streams remain valid across content replacement/disposal.</remarks>
public sealed class WebViewLocalContentResponse
{
    private readonly byte[] data;
    internal WebViewLocalContentResponse(int statusCode, string mimeType, byte[] data, IReadOnlyDictionary<string, string> headers)
    { StatusCode = statusCode; MimeType = mimeType; this.data = data; Headers = headers; }
    /// <summary>Gets the HTTP-style status (200, 400, 404, 405 or 410).</summary>
    public int StatusCode { get; }
    /// <summary>Gets the validated MIME type without charset parameters.</summary>
    public string MimeType { get; }
    /// <summary>Gets immutable security headers; no CORS access is granted.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
    /// <summary>Opens a read-only stream; ownership transfers to the caller or native engine.</summary>
    public Stream OpenRead() => new MemoryStream(data, 0, data.Length, writable: false, publiclyVisible: false);
}

/// <summary>Optional adapter contract for interpreting WebViewNavigationRequest.LocalContent.</summary>
/// <remarks>Existing baseline adapters need not implement this interface. No second native host is created.</remarks>
public interface IWebViewLocalContentPeer
{
    /// <summary>Gets whether the current engine supports fail-closed local resource interception.</summary>
    bool SupportsLocalContent { get; }
}
