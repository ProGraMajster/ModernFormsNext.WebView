using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebView;

namespace ModernFormsNext.WebView.BlazorProbe;

internal sealed class ManagerProbe : WebViewManager
{
    private readonly ResourceCatalog catalog;
    private readonly Action<Uri> navigate;
    private readonly IWebViewTransport? transport;
    internal int RejectedMessages { get; private set; }
    internal int RejectedAttachPages { get; private set; }

    internal ManagerProbe(IServiceProvider services, Dispatcher dispatcher, ResourceCatalog catalog,
        Action<Uri> navigate, IWebViewTransport? transport = null)
        : base(services, dispatcher, catalog.Content.Origin, new CatalogFileProvider(catalog), new JSComponentConfigurationStore(), "index.html")
    { this.catalog = catalog; this.navigate = navigate; this.transport = transport; }

    protected override void NavigateCore(Uri absoluteUri)
    {
        // A bounded research entrypoint; no arbitrary URI or app routing promise.
        if (absoluteUri != catalog.Content.GetUri("index.html")) throw new NotSupportedException("Research entrypoint only.");
        navigate(absoluteUri);
    }
    protected override void SendMessage(string message)
    {
        if (transport is null) throw new NotSupportedException("No browser transport in contract-only mode.");
        _ = SendAsync(message);
    }
    private async Task SendAsync(string message)
    {
        try { await transport!.PostMessageAsync(message); }
        catch { /* Research teardown; no page/native exception details are logged. */ }
    }
    internal void RejectUntrusted(WebViewMessageReceivedEventArgs message)
    {
        // The baseline never authenticates callers. Do not convert informational Source
        // or a token into permission to invoke MessageReceived/Blazor's native dispatch.
        if (!message.IsOriginAuthenticated)
        {
            RejectedMessages = Math.Min(256, RejectedMessages + 1);
            if (message.Message.StartsWith("__bwv:[\"AttachPage\",", StringComparison.Ordinal))
                RejectedAttachPages = Math.Min(256, RejectedAttachPages + 1);
            return;
        }
        throw new NotSupportedException("A privileged IPC policy has not been qualified.");
    }
    internal void CheckResource(string path)
    {
        if (!TryGetResponseContent(catalog.Content.GetUri(path).AbsoluteUri, false, out int status, out _, out var content, out _)
            || status != 200) throw new InvalidOperationException("Public WebViewManager asset response failed.");
        using (content)
        {
            using var copy = new MemoryStream(); content.CopyTo(copy);
            if (!copy.ToArray().SequenceEqual(catalog.Read(path))) throw new InvalidOperationException("Asset bytes changed.");
        }
    }
}
