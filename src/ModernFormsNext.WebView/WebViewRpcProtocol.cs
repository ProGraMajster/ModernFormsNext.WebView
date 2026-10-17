using System.Text;
using System.Text.Json;

namespace ModernFormsNext.WebView;

/// <summary>Experimental version-one, unprivileged app-to-page RPC wire contract.</summary>
/// <remarks>Correlation is not authentication. Install JavaScript explicitly per document; no C# handlers are exported.</remarks>
public static partial class WebViewRpcProtocol
{
    /// <summary>The supported envelope version.</summary>
    public const int Version = 1;
    /// <summary>Maximum UTF-8 bytes in an entire envelope, including payload.</summary>
    public const int MaxMessageBytes = 65_536;
    /// <summary>Maximum simultaneous requests per client/page endpoint.</summary>
    public const int MaxPendingRequests = 64;
    /// <summary>Maximum JSON nesting depth in an envelope.</summary>
    public const int MaxDepth = 32;
    /// <summary>Default request deadline in milliseconds, including submission.</summary>
    public const int DefaultTimeoutMilliseconds = 10_000;
    /// <summary>Largest permitted request deadline in milliseconds.</summary>
    public const int MaximumTimeoutMilliseconds = 120_000;

    internal static bool ValidMethod(string method) => method.Length is > 0 and <= 128 &&
        method.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-');
    internal static bool WithinLimit(string message) => message.Length <= MaxMessageBytes && Encoding.UTF8.GetByteCount(message) <= MaxMessageBytes;
    internal static string DocumentId(WebViewDocumentContext document) =>
        FormattableString.Invariant($"{document.PeerGeneration}:{document.RequestId}");

    internal static string Request(string id, string document, string method, JsonElement payload, int timeout)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteNumber("version", Version); writer.WriteString("type", "request");
            writer.WriteString("id", id); writer.WriteString("document", document); writer.WriteString("method", method);
            writer.WritePropertyName("payload"); payload.WriteTo(writer); writer.WriteNumber("timeoutMs", timeout); writer.WriteEndObject();
        }
        string message = Encoding.UTF8.GetString(stream.ToArray());
        if (!WithinLimit(message)) throw new WebViewRpcException(WebViewRpcErrorCategory.InvalidPayload);
        // Apply the same depth policy to outgoing typed data and incoming data.
        try { using var parsed = JsonDocument.Parse(message, new() { MaxDepth = MaxDepth }); }
        catch (JsonException) { throw new WebViewRpcException(WebViewRpcErrorCategory.InvalidPayload); }
        return message;
    }

    internal static string Cancel(string id, string document) =>
        "{\"version\":1,\"type\":\"cancel\",\"id\":\"" + id + "\",\"document\":\"" + document + "\"}";

    internal sealed record Reply(string Id, string Document, JsonElement Payload, string? Error);

    internal static Reply? ParseReply(string message)
    {
        if (!WithinLimit(message)) return null;
        try
        {
            using var json = JsonDocument.Parse(message, new() { MaxDepth = MaxDepth });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return null;
            bool error = type.GetString() == "error";
            if (!error && type.GetString() != "result") return null; // No inbound request dispatch.
            if (!Fields(root, "version", "type", "id", "document", error ? "error" : "payload") ||
                !root.GetProperty("version").TryGetInt32(out int version) || version != Version ||
                root.GetProperty("id").ValueKind != JsonValueKind.String || root.GetProperty("document").ValueKind != JsonValueKind.String) return null;
            string id = root.GetProperty("id").GetString()!, document = root.GetProperty("document").GetString()!;
            if (id.Length != 32 || !id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') || document.Length > 43) return null;
            if (!error) return new(id, document, root.GetProperty("payload").Clone(), null);
            var detail = root.GetProperty("error");
            if (!Fields(detail, "code") || detail.GetProperty("code").ValueKind != JsonValueKind.String) return null;
            string? code = detail.GetProperty("code").GetString();
            return code is "method_not_found" or "handler_error" or "busy" or "timeout" or "invalid_result" or "result_too_large"
                ? new(id, document, default, code) : null;
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static bool Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        int seen = 0;
        foreach (var property in value.EnumerateObject())
        {
            int index = Array.IndexOf(names, property.Name);
            if (index < 0 || (seen & (1 << index)) != 0) return false;
            seen |= 1 << index;
        }
        return seen == (1 << names.Length) - 1;
    }
}

/// <summary>Fixed RPC failure categories without native/page exception details.</summary>
public enum WebViewRpcErrorCategory
{
    /// <summary>The local or page deadline elapsed; submitted effects cannot be rolled back.</summary>
    Timeout,
    /// <summary>Submission to the borrowed transport failed.</summary>
    Transport,
    /// <summary>Outgoing typed data exceeds the wire policy or cannot be serialized.</summary>
    InvalidPayload,
    /// <summary>A correlated response could not be converted to the explicitly requested type.</summary>
    InvalidResponse,
    /// <summary>The JavaScript endpoint returned a fixed allowed error code.</summary>
    RemoteError
}

/// <summary>A sanitized RPC failure; never includes page payloads, error messages or inner exceptions.</summary>
public sealed class WebViewRpcException : Exception
{
    /// <summary>Creates a fixed-message failure in the given category.</summary>
    public WebViewRpcException(WebViewRpcErrorCategory category) : base("Browser RPC failed (" + category + ").") => Category = category;
    /// <summary>Gets the fixed failure category.</summary>
    public WebViewRpcErrorCategory Category { get; }
    /// <summary>Gets a fixed allowlisted page error code, or null for local failures.</summary>
    public string? RemoteCode { get; internal init; }
}
