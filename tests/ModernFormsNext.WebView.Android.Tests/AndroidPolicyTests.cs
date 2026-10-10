using System.Text.Json;
using Xunit;

namespace ModernFormsNext.WebView.Android.Tests;

public sealed class AndroidPolicyTests
{
    [Theory]
    [InlineData(-2, WebViewErrorCategory.HostNotFound)]
    [InlineData(-6, WebViewErrorCategory.Connection)]
    [InlineData(-8, WebViewErrorCategory.Connection)]
    [InlineData(-11, WebViewErrorCategory.Certificate)]
    [InlineData(-10, WebViewErrorCategory.InvalidAddress)]
    [InlineData(-12, WebViewErrorCategory.InvalidAddress)]
    [InlineData(-7, WebViewErrorCategory.Network)]
    [InlineData(999, WebViewErrorCategory.Unknown)]
    public void ErrorsMapToStableNeutralCategories(int code, WebViewErrorCategory category)
        => Assert.Equal(category, AndroidWebViewPolicy.MapError(code));
    [Theory]
    [InlineData("quote\"\nnext")]
    [InlineData("</script><script>alert(1)</script>")]
    [InlineData("\\\r\n\u2028")]
    [InlineData("")]
    public void MessageContentIsOneJsonStringLiteral(string message)
    {
        string script = AndroidWebViewPolicy.CreateMessageScript(message);
        string literal = script[(script.IndexOf("data:", StringComparison.Ordinal) + 5)..script.LastIndexOf("}));null", StringComparison.Ordinal)];
        Assert.Equal(message, JsonSerializer.Deserialize<string>(literal));
        Assert.DoesNotContain("</script>", literal);
    }
}
