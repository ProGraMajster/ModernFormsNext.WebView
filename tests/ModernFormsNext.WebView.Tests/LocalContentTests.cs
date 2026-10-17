using System.Text;

namespace ModernFormsNext.WebView.Tests;

public sealed class LocalContentTests
{
    private static WebViewLocalContent Content() => new(new Dictionary<string, byte[]>
    {
        ["index.html"] = Encoding.UTF8.GetBytes("<h1>Public</h1>"),
        ["assets/app.js"] = Encoding.UTF8.GetBytes("window.answer=42;"),
        ["assets/app.css"] = Encoding.UTF8.GetBytes("h1{color:navy}"),
        ["assets/image.png"] = [137, 80, 78, 71]
    });

    [Theory]
    [InlineData("../private.html")]
    [InlineData("a/../private.html")]
    [InlineData("./index.html")]
    [InlineData("/index.html")]
    [InlineData("//server/index.html")]
    [InlineData("C:/index.html")]
    [InlineData("a\\index.html")]
    [InlineData("%2e%2e/private.html")]
    [InlineData("%252e%252e/private.html")]
    [InlineData("a/%2findex.html")]
    [InlineData("index.html?private")]
    [InlineData("index.html#private")]
    [InlineData("a//index.html")]
    [InlineData("a./index.html")]
    [InlineData("a\0.html")]
    [InlineData("zażółć.html")]
    [InlineData("a b.html")]
    [InlineData("")]
    public void ConstructionAndRequestResolutionRejectNoncanonicalPaths(string path)
    {
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(new Dictionary<string, byte[]> { [path] = [] }));
        var content = Content();
        Assert.Throws<ArgumentException>(() => content.GetUri(path));
        Assert.Equal(400, content.CreateResponse(content.Origin.AbsoluteUri + path).StatusCode);
    }

    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("assets/app.js", "text/javascript")]
    [InlineData("assets/app.css", "text/css")]
    [InlineData("assets/image.png", "image/png")]
    public void ExactPublicPathsHaveExplicitMimeAndIndependentStreams(string path, string mime)
    {
        var content = Content();
        var response = content.CreateResponse(content.GetUri(path).AbsoluteUri);
        Assert.Equal(200, response.StatusCode); Assert.Equal(mime, response.MimeType);
        using var a = response.OpenRead(); using var b = response.OpenRead();
        Assert.NotSame(a, b); Assert.False(a.CanWrite); Assert.True(a.Length > 0);
        a.ReadByte(); Assert.Equal(0, b.Position);
        Assert.Throws<NotSupportedException>(() => a.WriteByte(0));
        Assert.Throws<UnauthorizedAccessException>(() => ((MemoryStream)a).GetBuffer());
    }

    [Fact]
    public void ContentIsCopiedAndOriginsIsolateCollections()
    {
        byte[] input = [1, 2, 3];
        var dictionary = new Dictionary<string, byte[]> { ["a.png"] = input };
        var first = new WebViewLocalContent(dictionary); input[0] = 99; dictionary.Clear();
        using var stream = first.CreateResponse(first.GetUri("a.png").AbsoluteUri).OpenRead();
        Assert.Equal(1, stream.ReadByte());
        var second = Content(); Assert.NotEqual(first.Origin, second.Origin);
        Assert.Equal(404, second.CreateResponse(first.GetUri("a.png").AbsoluteUri).StatusCode);
        Assert.Equal(404, first.CreateResponse(first.Origin + "private.txt").StatusCode);
        Assert.Throws<ArgumentException>(() => first.GetUri("private.txt"));
    }

    [Theory]
    [InlineData("http://")]
    [InlineData("https://user@")]
    [InlineData("https://attacker.test/?next=https://")]
    public void SchemeCredentialsAndEmbeddedOriginAreNotAuthority(string prefix)
    {
        var content = Content();
        Assert.Equal(404, content.CreateResponse(prefix + content.Origin.Host + "/index.html").StatusCode);
        Assert.Equal(404, content.CreateResponse("https://" + content.Origin.Host + ":443/index.html").StatusCode);
        Assert.Equal(404, content.CreateResponse(content.Origin.AbsoluteUri.TrimEnd('/') + ".attacker.test/index.html").StatusCode);
    }

    [Fact]
    public void ReservedNamespaceIncludesInactiveOriginsButDoesNotMatchLookalikes()
    {
        Assert.True(WebViewLocalContent.IsLocalUri(Content().Origin.AbsoluteUri));
        Assert.True(WebViewLocalContent.IsLocalUri("http://old.mfn-app.invalid/"));
        Assert.True(WebViewLocalContent.IsLocalUri("https://old.mfn-app.invalid./"));
        Assert.True(WebViewLocalContent.IsLocalUri("https://mfn-app.invalid/"));
        Assert.False(WebViewLocalContent.IsLocalUri("https://mfn-app.invalid.attacker.test/"));
        Assert.False(WebViewLocalContent.IsLocalUri("https://attacker.test/?mfn-app.invalid"));
        Assert.False(WebViewLocalContent.IsLocalUri("not a URI"));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("OPTIONS")]
    [InlineData("get")]
    public void NonReadMethodsAreDenied(string method)
    {
        var content = Content();
        Assert.Equal(405, content.CreateResponse(content.GetUri("index.html").AbsoluteUri, method).StatusCode);
    }

    [Fact]
    public void FixedSecurityHeadersAlsoApplyToEmptyHeadAndErrorResponses()
    {
        var content = Content();
        foreach (var response in new[] { content.CreateResponse(content.GetUri("index.html").AbsoluteUri, "HEAD"),
            content.CreateResponse(content.Origin + "absent.html"), WebViewLocalContent.ErrorResponse() })
        {
            using var stream = response.OpenRead(); Assert.Equal(0, stream.Length);
            Assert.Equal("nosniff", response.Headers["X-Content-Type-Options"]);
            Assert.Equal("no-store", response.Headers["Cache-Control"]);
            Assert.Equal("same-origin", response.Headers["Cross-Origin-Resource-Policy"]);
            Assert.Contains("frame-ancestors 'none'", response.Headers["Content-Security-Policy"]);
            Assert.Contains("connect-src 'none'", response.Headers["Content-Security-Policy"]);
            Assert.DoesNotContain("Access-Control-Allow-Origin", response.Headers.Keys);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)response.Headers).Add("unsafe", "value"));
        }
    }

    [Fact]
    public void SizeCountAndUnsupportedFormatsAreBounded()
    {
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(new Dictionary<string, byte[]>()));
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(new Dictionary<string, byte[]> { ["x.svg"] = [] }));
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(new Dictionary<string, byte[]> { ["x.png"] = new byte[4 * 1024 * 1024 + 1] }));
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(Enumerable.Range(0, 1025).ToDictionary(i => $"{i}.txt", _ => Array.Empty<byte>())));
        Assert.Throws<ArgumentException>(() => new WebViewLocalContent(Enumerable.Range(0, 5).ToDictionary(i => $"{i}.txt", _ => new byte[4 * 1024 * 1024])));
        Assert.Throws<ArgumentException>(() => Content().GetUri(new string('a', 1025) + ".html"));
    }

    [Fact]
    public async Task ConcurrentInterceptionNeedsNoUiThreadOrMutableProvider()
    {
        var content = Content(); string address = content.GetUri("index.html").AbsoluteUri;
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            using var reader = new StreamReader(content.CreateResponse(address).OpenRead());
            Assert.Equal("<h1>Public</h1>", reader.ReadToEnd());
        })));
    }
}
