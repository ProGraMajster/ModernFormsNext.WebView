using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.Android;

/// <summary>Creates fresh Activity-bound browser peers without owning an Activity or View.</summary>
public sealed class AndroidWebViewProvider : IWebViewPeerFactoryProvider
{
    /// <summary>Registers the adapter explicitly after AndroidWindowKit.Initialize at process startup.</summary>
    public static AndroidWebViewProvider Register()
    {
        if (WindowKitBackendRegistry.Current?.IsInitialized != true)
            throw new InvalidOperationException("Initialize the Android WindowKit backend before registering WebView.");
        Dispatcher.UIThread.VerifyAccess();
        var provider = new AndroidWebViewProvider();
        PlatformServiceRegistry.Register<IWebViewPeerFactoryProvider>(provider);
        return provider;
    }
    /// <inheritdoc/>
    public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (site is not AndroidNativeViewSite android || !site.IsCurrent)
            throw new ArgumentException("A current Android native hosting site is required.", nameof(site));
        ArgumentNullException.ThrowIfNull(client);
        return new AndroidWebViewPeer(android, client);
    }
}
