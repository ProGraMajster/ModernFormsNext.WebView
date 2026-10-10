using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Backend.Windows;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.WebView.Windows;

/// <summary>Configures the application-owned WebView2 environment.</summary>
public sealed class WindowsWebViewOptions
{
    /// <summary>Gets or sets the browser profile directory; null selects an app-directory-keyed LocalAppData folder.</summary>
    /// <remarks>Set a unique temporary directory for runtime tests. The provider never deletes application profiles.</remarks>
    public string? UserDataFolder { get; init; }
}

/// <summary>Shares one lazy WebView2 environment across current application peers.</summary>
/// <remarks>
/// Register once on the UI thread. The provider owns no controllers; each native session owns
/// its peer/controller. Environment COM references live with this application provider. No
/// polling or runtime installation occurs. Configuration cannot change after registration.
/// </remarks>
public sealed class WindowsWebViewProvider : IWebViewPeerFactoryProvider
{
    private readonly string userDataFolder;
    private Task<CoreWebView2Environment>? environment;

    /// <summary>Creates an application-scoped provider without starting the browser runtime.</summary>
    public WindowsWebViewProvider(WindowsWebViewOptions? options = null)
    {
        string appKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..16];
        userDataFolder = options?.UserDataFolder ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModernFormsNext.WebView", appKey);
    }

    /// <summary>Registers the Windows browser provider in the existing MFN platform registry.</summary>
    /// <remarks>Call explicitly once at startup, before displaying WebView controls.</remarks>
    public static WindowsWebViewProvider Register(WindowsWebViewOptions? options = null)
    {
        if (WindowKitBackendRegistry.Current?.IsInitialized != true)
            throw new InvalidOperationException("Initialize the Windows WindowKit backend before registering WebView.");
        Dispatcher.UIThread.VerifyAccess();
        var provider = new WindowsWebViewProvider(options);
        PlatformServiceRegistry.Register<IWebViewPeerFactoryProvider>(provider);
        return provider;
    }

    internal Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        // Failures are shared with existing waiters; a new lease may retry. No timer or
        // automatic peer retry loop is introduced after a failed initialization.
        if (environment is { IsFaulted: true } or { IsCanceled: true }) environment = null;
        return environment ??= CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }

    /// <inheritdoc/>
    public IWebViewPeer CreatePeer(INativeViewSite site, IWebViewPeerClient client)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (site is not WindowsNativeViewSite windows || !site.IsCurrent)
            throw new ArgumentException("A current Windows native hosting site is required.", nameof(site));
        ArgumentNullException.ThrowIfNull(client);
        return new WindowsWebViewPeer(this, windows, client);
    }
}
