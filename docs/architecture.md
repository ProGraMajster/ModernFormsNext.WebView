# Architecture and ownership

The dependency pin is main framework merge commit
`69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3` (#60 / PR #197). The main repository is
read-only for this feature. Its public native-hosting and API-review documents are
authoritative for placement, airspace and session ownership.

```text
WebView : NativeViewHost
  -> feature factory -> registered IWebViewPeerFactoryProvider
  -> framework-owned current WindowsNativeViewSite / AndroidNativeViewSite
  -> owned browser peer lease
  -> browser controller / fresh Android View
```

PlatformServiceRegistry is the existing MFN registry. No reflection, assembly scan,
module initializer or second locator selects the browser provider. A parameterless
control installs a harmless feature factory; it resolves the provider only during
actual hosting. An explicit-provider constructor supports scoped consumers and tests.
The application owns the provider; NativeViewHost owns the session and disposable peer.

The shared assembly targets net10.0 and contains control state, neutral contracts,
events, results and detached diagnostics. Windows targets net10.0-windows10.0.17763.0.
Android targets net10.0-android (minSdk 23); its additional net10.0 target contains only
deterministic policy so tests need no device. Samples have the corresponding platform TFM.

NativeViewHost owns ancestor subscriptions, pixel placement, scrolling, clip intersection,
visibility/enabled state, DPI/density, canonical focus/validation and cross-window/Activity
session recreation. Adapters resize only their content inside the supplied container.
They discover no top-level HWND and implement no SetWindowPos/layout/focus manager.
Same-window Skia content cannot overlay a native browser; use MFN popup/modal top-levels.
Opacity, transforms, rounded clipping and effects have the main host's documented limits.

Shared lease generation plus site.IsCurrent rejects retired callbacks. Monotonic request
identity rejects superseded navigation, history and messages. Document cancellation tokens
bound scripts/messages to the current request; no growing pending-task list exists in shared.
Terminal callbacks settle loading in finally, even if observers throw or navigate again.
Synchronous user observer exceptions propagate; Windows initialization notifications run
outside the native initialization catch, through the established dispatcher policy.

Windows maintains at most one command awaiting native NavigationStarting ID acknowledgement;
new commands coalesce until it is acknowledged. Redirects retain the same original identity.
Android has no native request IDs. Its interrupted programmatic navigation replaces the
browser content View in the existing site, revoking the old callback generation. This is
feature content lifetime, not another host session; history resets in that case.

Disposal revokes callbacks before releasing engine subscriptions/controllers/clients/bridges.
There is no feature finalizer; UI/native cleanup belongs to explicit UI-thread disposal.
Android views are never migrated between Activity contexts. App-level engine state is not
aggressively reset: disposal does not clear global cookies/cache or other controls' state.

Packaging remains disabled. ModernFormsNextSourceRoot is an explicit development
input with no machine path in project files. It is required at this stage because no package
version is selected as containing #60. A future packaging change can replace the conditional
source references with PackageReferences under an explicit supported framework version.
