using System.Text.Json;

namespace ModernFormsNext.WebView.Android;

/// <summary>Deterministic Android adapter policy, independently testable without a device.</summary>
public static class AndroidWebViewPolicy
{
    /// <summary>Builds a safely escaped string MessageEvent dispatch, with no raw string concatenation.</summary>
    public static string CreateMessageScript(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return "window.dispatchEvent(new MessageEvent('message',{data:" + JsonSerializer.Serialize(message) + "}));null";
    }

    /// <summary>Maps stable WebViewClient integer error codes into neutral categories.</summary>
    public static WebViewErrorCategory MapError(int errorCode) => errorCode switch
    {
        -2 => WebViewErrorCategory.HostNotFound,
        -6 or -8 => WebViewErrorCategory.Connection,
        -11 => WebViewErrorCategory.Certificate,
        -10 or -12 => WebViewErrorCategory.InvalidAddress,
        -7 => WebViewErrorCategory.Network,
        _ => WebViewErrorCategory.Unknown
    };
}
