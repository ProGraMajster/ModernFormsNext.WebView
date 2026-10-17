# Windows adapter

The official `Microsoft.Web.WebView2` SDK is pinned centrally to **1.0.4258.31**, the stable
release available during this audit. The selected Core APIs are stable. The extension targets
Windows 10 1809 (10.0.17763) or later; the main Windows backend itself is plain net10.0 while
main framework also offers net10.0-windows. Use a supported Windows 10/11 installation and
an installed Evergreen WebView2 Runtime compatible with the selected SDK/API surface.
Evergreen installation/bundling is not performed by this repository.
See [Microsoft SDK versioning](https://learn.microsoft.com/microsoft-edge/webview2/concepts/versioning).

Startup on the STA UI thread:

```csharp
WindowsPlatformBootstrap.Initialize();
WindowsWebViewProvider.Register(new WindowsWebViewOptions { UserDataFolder = appProfileDirectory });
Application.Run(new MainForm());
```

Register rejects startup before backend initialization, avoiding creation of an unconfigured
Dispatcher. The application owns one immutable provider/configuration with one lazy async
CoreWebView2Environment shared by its leases. A failed task is shared with existing waiters;
a fresh lease can retry. Separate explicit providers/profile directories isolate tests/configuration.

The factory returns a lease immediately. Environment/controller creation remains asynchronous.
After every await the established dispatcher is used and site.IsCurrent/client generation are
checked. Late controllers are closed. Content size is retained until initialization completes.
Only WindowsNativeViewSite.ParentWindow is used for parenting. Controller.Bounds covers content
inside that container; framework container position, clip, visibility, enabled state and DPI are
not duplicated. Deferred focus checks site.IsFocused before controller.MoveFocus.

NavigationStarting maps native NavigationId to the original logical request. The acknowledgement
queue prevents a delayed event from acquiring a newer request ID. PermissionRequested denies,
NewWindowRequested is handled without creating a window, DownloadStarting cancels, host objects
and DevTools are disabled. TLS validation is never bypassed. Core event subscriptions and the
document bridge script are removed before controller.Close. Core/controller references are released;
the application provider retains only the shared environment task, never disposed controllers.

The default profile directory is under LocalAppData/ModernFormsNext.WebView with a stable hash
of the application directory. Applications sharing that directory share that default profile;
choose an explicit app-specific profile to control isolation. It never uses the source working
directory. Production profiles are persistent application data and are not deleted on Dispose.
Native validation uses a unique temporary profile and bounded cleanup after closing controllers.

The [#15 hardening and decision](issue15-windows-message-hardening.md) binds private incoming
packets to a native-prepared token and checks controlled local-resource Source by parsed origin.
Stop, canceled/failed navigation, JS disable and controller cleanup revoke correlation. A stale
completion cannot restore it. Same-origin child-to-parent helper calls still arrive as untrusted
baseline data; AuthenticatedMessaging remains unavailable and no C# handler is exported.

The official package unconditionally adds WPF/WinForms wrapper references. Build targets remove
those unused wrappers before assembly resolution, preserving only Core plus the official native
loader. This framework has no WPF/WinForms UI integration.

`--validate` on the sample runs real offline scripting/messaging/lifetime scenarios plus a random
loopback-port HTTP server for URI/history tests and cross-window replay. The server shuts down
deterministically and has no persistent background process. Runtime absence is failure/unavailable
evidence, never a simulated pass. Physical keyboard/visual/profiler qualification is separate.
