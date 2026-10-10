using Microsoft.Web.WebView2.Core;

namespace ModernFormsNext.WebView.Windows;

internal static class WindowsWebViewPolicy
{
    internal static WebViewErrorCategory MapError(CoreWebView2WebErrorStatus error) => error switch
    {
        CoreWebView2WebErrorStatus.OperationCanceled => WebViewErrorCategory.Canceled,
        CoreWebView2WebErrorStatus.HostNameNotResolved => WebViewErrorCategory.HostNotFound,
        CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or CoreWebView2WebErrorStatus.CertificateExpired
            or CoreWebView2WebErrorStatus.ClientCertificateContainsErrors or CoreWebView2WebErrorStatus.CertificateIsInvalid => WebViewErrorCategory.Certificate,
        CoreWebView2WebErrorStatus.CannotConnect or CoreWebView2WebErrorStatus.ConnectionAborted
            or CoreWebView2WebErrorStatus.ConnectionReset or CoreWebView2WebErrorStatus.Timeout => WebViewErrorCategory.Connection,
        CoreWebView2WebErrorStatus.Disconnected or CoreWebView2WebErrorStatus.ServerUnreachable => WebViewErrorCategory.Network,
        _ => WebViewErrorCategory.Unknown
    };
}
