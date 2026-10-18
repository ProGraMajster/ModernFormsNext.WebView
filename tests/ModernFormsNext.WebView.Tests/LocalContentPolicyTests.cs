using System.Text;

namespace ModernFormsNext.WebView.Tests;

public sealed class LocalContentPolicyTests
{
    private const string OriginalCsp = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; " +
        "connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; " +
        "form-action 'none'; frame-ancestors 'none'";
    private static WebViewLocalContent Content(WebViewLocalContentPolicy policy) => new(
        new Dictionary<string, byte[]> { ["index.html"] = Encoding.UTF8.GetBytes("<p>public</p>"), ["data.json"] = "[42]"u8.ToArray() }, policy);

    [Fact]
    public void OriginalConstructorAndDefaultEquivalentKeepExactHeaderAndPresetsStayIndependent()
    {
        var original = new WebViewLocalContent(new Dictionary<string, byte[]> { ["index.html"] = [] });
        Assert.Same(WebViewLocalContentPolicy.Default, original.Policy);
        Assert.Equal(OriginalCsp, original.CreateResponse(original.GetUri("index.html").AbsoluteUri).Headers["Content-Security-Policy"]);
        Assert.Equal(OriginalCsp, new WebViewLocalContentPolicy().ContentSecurityPolicy);
        Assert.Equal(OriginalCsp.Replace("connect-src 'none'", "connect-src 'self'", StringComparison.Ordinal)
            .Replace("base-uri 'none'", "base-uri 'self'", StringComparison.Ordinal), WebViewLocalContentPolicy.Application.ContentSecurityPolicy);
        Assert.Equal(OriginalCsp, WebViewLocalContentPolicy.Default.ContentSecurityPolicy);
        Assert.Throws<ArgumentNullException>(() => new WebViewLocalContent(new Dictionary<string, byte[]>(), null!));
    }

    [Theory]
    [InlineData(0, "scripts", -1)] [InlineData(0, "scripts", 2)]
    [InlineData(1, "styles", -1)] [InlineData(1, "styles", 2)]
    [InlineData(2, "images", -1)] [InlineData(2, "images", 2)]
    [InlineData(3, "connections", -1)] [InlineData(3, "connections", 2)]
    [InlineData(4, "baseUri", -1)] [InlineData(4, "baseUri", 2)]
    public void UndefinedSelectorsFailAtConstruction(int index, string parameter, int value)
    {
        var sources = new[] { WebViewLocalContentSource.Self, WebViewLocalContentSource.Self, WebViewLocalContentSource.Self,
            WebViewLocalContentSource.None, WebViewLocalContentSource.None };
        sources[index] = (WebViewLocalContentSource)value;
        Assert.Equal(parameter, Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WebViewLocalContentPolicy(sources[0], sources[1], sources[2], sources[3], sources[4])).ParamName);
    }

    [Theory]
    [InlineData("scripts", "script-src 'self'", "script-src 'none'")]
    [InlineData("styles", "style-src 'self'", "style-src 'none'")]
    [InlineData("images", "img-src 'self'", "img-src 'none'")]
    [InlineData("connections", "connect-src 'none'", "connect-src 'self'")]
    [InlineData("base", "base-uri 'none'", "base-uri 'self'")]
    public void EachSelectorChangesOnlyItsDeclaredDirective(string selector, string before, string after)
    {
        var policy = selector switch
        {
            "scripts" => new WebViewLocalContentPolicy(scripts: WebViewLocalContentSource.None),
            "styles" => new WebViewLocalContentPolicy(styles: WebViewLocalContentSource.None),
            "images" => new WebViewLocalContentPolicy(images: WebViewLocalContentSource.None),
            "connections" => new WebViewLocalContentPolicy(connections: WebViewLocalContentSource.Self),
            _ => new WebViewLocalContentPolicy(baseUri: WebViewLocalContentSource.Self)
        };
        Assert.Equal(OriginalCsp.Replace(before, after, StringComparison.Ordinal), policy.ContentSecurityPolicy);
    }

    [Theory]
    [InlineData("index.html", "GET", 200)] [InlineData("index.html", "HEAD", 200)]
    [InlineData("absent.json", "GET", 404)] [InlineData("data.json?x", "GET", 400)]
    [InlineData("data.json", "POST", 405)]
    public void ActivePolicyAlsoAppliesToHeadAndSameOriginDenials(string path, string method, int status)
    {
        var content = Content(WebViewLocalContentPolicy.Application);
        var response = content.CreateResponse(content.Origin.AbsoluteUri + path, method);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(content.Policy.ContentSecurityPolicy, response.Headers["Content-Security-Policy"]);
        Assert.Equal("nosniff", response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", response.Headers["X-Frame-Options"]);
        Assert.DoesNotContain("Access-Control-Allow-Origin", response.Headers.Keys);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)response.Headers)["Content-Security-Policy"] = "default-src *");
        using var stream = response.OpenRead();
        if (method == "HEAD" || status != 200) Assert.Equal(0, stream.Length);
    }

    [Theory]
    [InlineData("http://{0}/data.json")] [InlineData("https://{0}:444/data.json")]
    [InlineData("https://user@{0}/data.json")] [InlineData("https://{0}.attacker.invalid/data.json")]
    public void ApplicationPolicyCannotBroadenAuthorityOrInactiveDenial(string format)
    {
        var content = Content(WebViewLocalContentPolicy.Application);
        var response = content.CreateResponse(string.Format(System.Globalization.CultureInfo.InvariantCulture, format, content.Origin.Host));
        Assert.Equal(404, response.StatusCode); Assert.Equal(OriginalCsp, response.Headers["Content-Security-Policy"]);
        Assert.Equal(OriginalCsp, WebViewLocalContent.ErrorResponse().Headers["Content-Security-Policy"]);
    }

    [Theory]
    [InlineData("../data.json")] [InlineData("%2fdata.json")] [InlineData("data.json\r\nContent-Security-Policy:x")]
    [InlineData("data.json#x")] [InlineData("a\\data.json")]
    public void OptInDoesNotChangeCanonicalPathOrHeaderInjectionRules(string path)
    {
        var content = Content(WebViewLocalContentPolicy.Application);
        Assert.Equal(400, content.CreateResponse(content.Origin.AbsoluteUri + path).StatusCode);
        Assert.Throws<ArgumentException>(() => content.GetUri(path));
    }

    [Fact]
    public async Task ConcurrentCatalogsDoNotShareMutablePolicyOrResponseState()
    {
        var strict = Content(WebViewLocalContentPolicy.Default); var app = Content(WebViewLocalContentPolicy.Application);
        var noScript = Content(new WebViewLocalContentPolicy(scripts: WebViewLocalContentSource.None));
        await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() =>
        {
            var content = i % 3 == 0 ? strict : i % 3 == 1 ? app : noScript;
            var response = content.CreateResponse(content.GetUri("data.json").AbsoluteUri);
            Assert.Equal(content.Policy.ContentSecurityPolicy, response.Headers["Content-Security-Policy"]);
            Assert.Equal("application/json", response.MimeType);
            using var stream = response.OpenRead(); Assert.Equal(4, stream.Length);
        })));
        Assert.NotEqual(strict.Origin, app.Origin);
        Assert.Equal(OriginalCsp, strict.Policy.ContentSecurityPolicy);
    }
}
