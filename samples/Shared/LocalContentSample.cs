namespace ModernFormsNext.WebView.Sample;

internal static class LocalContentSample
{
    internal static WebViewLocalContent Create()
    {
        byte[] Read(string suffix)
        {
            var assembly = typeof(LocalContentSample).Assembly;
            using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith("LocalContent." + suffix, StringComparison.Ordinal)))!;
            using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray();
        }
        return new(new Dictionary<string, byte[]>
        {
            ["index.html"] = Read("index.html"), ["app.css"] = Read("app.css"), ["app.js"] = Read("app.js"),
            ["pixel.png"] = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLttAAAAABJRU5ErkJggg=="),
            ["frame.html"] = System.Text.Encoding.UTF8.GetBytes("<script src='frame.js'></script>"),
            ["frame.js"] = System.Text.Encoding.UTF8.GetBytes("parent.frameExecuted=true;")
        });
    }
}
