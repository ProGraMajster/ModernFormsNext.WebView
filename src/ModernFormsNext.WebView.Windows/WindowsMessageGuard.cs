using System.Text.Json;

namespace ModernFormsNext.WebView.Windows;

// Binds the private wire packet to the existing logical request, not a second document
// counter. Stops stale packet ID substitution, without authenticating a script caller:
// a same-origin frame can invoke the current parent's helper. UI thread only.
internal sealed class WindowsMessageGuard
{
    private long requestId;
    private string? token;
    internal string Prepare(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (requestId != id || token is null) { requestId = id; token = Guid.NewGuid().ToString("N"); }
        return token;
    }
    internal bool TryGetToken(long id, out string value)
    { value = requestId == id ? token ?? "" : ""; return value.Length != 0; }
    internal void Revoke() { requestId = 0; token = null; }
    internal void Revoke(long id) { if (requestId == id) Revoke(); }

    internal bool TryRead(string wire, Uri? nativeSource, Uri? requiredOrigin, out long id, out string message)
    {
        id = 0; message = "";
        if (token is null || (requiredOrigin is not null && !SameOrigin(nativeSource, requiredOrigin))) return false;
        try
        {
            using var json = JsonDocument.Parse(wire);
            var packet = json.RootElement;
            if (packet.ValueKind != JsonValueKind.Array || packet.GetArrayLength() != 3 ||
                packet[0].ValueKind != JsonValueKind.Number || !packet[0].TryGetInt64(out long candidate) || candidate != requestId ||
                packet[1].ValueKind != JsonValueKind.String || packet[1].GetString() != token || packet[2].ValueKind != JsonValueKind.String) return false;
            id = candidate; message = packet[2].GetString()!; return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool SameOrigin(Uri? source, Uri required) => source is { IsAbsoluteUri: true } && required.IsAbsoluteUri &&
        source.UserInfo.Length == 0 && source.Scheme == "https" && required.Scheme == "https" &&
        source.Port == required.Port && string.Equals(source.IdnHost, required.IdnHost, StringComparison.OrdinalIgnoreCase);
}
