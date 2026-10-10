using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WebView.Windows;
using Xunit;

namespace ModernFormsNext.WebView.Windows.Tests;

public sealed class ErrorMappingTests
{
    [Theory]
    [InlineData(CoreWebView2WebErrorStatus.OperationCanceled, WebViewErrorCategory.Canceled)]
    [InlineData(CoreWebView2WebErrorStatus.HostNameNotResolved, WebViewErrorCategory.HostNotFound)]
    [InlineData(CoreWebView2WebErrorStatus.CertificateExpired, WebViewErrorCategory.Certificate)]
    [InlineData(CoreWebView2WebErrorStatus.CertificateIsInvalid, WebViewErrorCategory.Certificate)]
    [InlineData(CoreWebView2WebErrorStatus.ClientCertificateContainsErrors, WebViewErrorCategory.Certificate)]
    [InlineData(CoreWebView2WebErrorStatus.CannotConnect, WebViewErrorCategory.Connection)]
    [InlineData(CoreWebView2WebErrorStatus.Timeout, WebViewErrorCategory.Connection)]
    [InlineData(CoreWebView2WebErrorStatus.Disconnected, WebViewErrorCategory.Network)]
    [InlineData((CoreWebView2WebErrorStatus)int.MaxValue, WebViewErrorCategory.Unknown)]
    public void NativeErrorsMapToStableSharedCategories(CoreWebView2WebErrorStatus native, WebViewErrorCategory expected)
        => Assert.Equal(expected, WindowsWebViewPolicy.MapError(native));
}
