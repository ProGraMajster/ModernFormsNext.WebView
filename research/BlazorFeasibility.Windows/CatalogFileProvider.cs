using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace ModernFormsNext.WebView.BlazorProbe;

internal sealed class ResourceCatalog
{
    internal readonly WebViewLocalContent Content;
    internal readonly string[] Paths;
    internal ResourceCatalog(string razorHtml)
    {
        byte[] Read(string name)
        {
            using var stream = typeof(ResourceCatalog).Assembly.GetManifestResourceStream("Probe." + name)
                ?? throw new InvalidOperationException("Missing research package asset: " + name);
            using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray();
        }
        var resources = new Dictionary<string, byte[]>
        {
            ["index.html"] = Encoding.UTF8.GetBytes("<!doctype html><html><head><script src='bootstrap.js'></script>" +
                "<base href='/'><script src='_framework/blazor.webview.js' autostart='false'></script></head><body>" + razorHtml + "</body></html>"),
            ["bootstrap.js"] = Read("bootstrap.js"),
            ["_framework/blazor.webview.js"] = Read("blazor.webview.js"),
            ["_framework/blazor.modules.json"] = Read("blazor.modules.json")
        };
        Paths = resources.Keys.ToArray(); Content = new(resources);
    }
    internal byte[] Read(string path)
    {
        using var stream = Content.CreateResponse(Content.GetUri(path).AbsoluteUri).OpenRead();
        using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray();
    }
}

// Required IFileProvider seam over the existing immutable resource catalog, with no
// filesystem mapping, directory enumeration, watcher or parallel resource policy.
internal sealed class CatalogFileProvider(ResourceCatalog catalog) : IFileProvider
{
    public IFileInfo GetFileInfo(string subpath)
    {
        string path = subpath.StartsWith('/') ? subpath[1..] : subpath;
        return catalog.Paths.Contains(path, StringComparer.Ordinal) ? new CatalogFile(catalog, path) : new NotFoundFileInfo(subpath);
    }
    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;
    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    private sealed class CatalogFile(ResourceCatalog catalog, string path) : IFileInfo
    {
        public bool Exists => true;
        public long Length { get { using var stream = CreateReadStream(); return stream.Length; } }
        public string? PhysicalPath => null;
        public string Name => System.IO.Path.GetFileName(path);
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => catalog.Content.CreateResponse(catalog.Content.GetUri(path).AbsoluteUri).OpenRead();
    }
}
