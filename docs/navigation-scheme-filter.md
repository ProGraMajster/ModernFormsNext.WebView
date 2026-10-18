# Per-view navigation scheme filter (#46)

## Inventory before implementation

Only the navigation row is implemented in this increment. Other rows describe future
scope decisions, not available options. No global security-disable or caller-authentication
flag is introduced.

| Area | Windows scope | Android scope/floor | Decision/default/risk |
| --- | --- | --- | --- |
| Main navigation/redirects | Core NavigationStarting per view | API24+ request main-frame metadata; POST uses later OnPageStarted fallback | Implement typed scheme filter for app URI commands/reported top-level callbacks; no network firewall |
| New windows | NewWindowRequested per view | WebChromeClient creation per view, with WebSettings | Existing deny; future explicit opt-in needs owned windows/lifetime and engine qualification |
| Downloads | DownloadStarting per view | DownloadListener per view; DownloadManager is a separate app service | Existing deny/no automatic save; future explicit destination and cancellation |
| Page permissions | Per-view requests, persisted grants may be profile-wide | WebChromeClient requests plus separate OS runtime permission (API23+) | Existing deny; future granular consent, no implicit OS/profile grants |
| Storage/cookies | Profile/environment scope; provider currently shares its environment | CookieManager/data shared in the process; no product per-view cookie store | Advanced future profile design; do not disguise shared state as per-view isolation |
| DevTools | Core setting per view; remote-debug process options are separate | Static process-wide API19 setting; provider113 can auto-enable in debuggable apps | Future explicit development choice; no hidden global mutation |
| Profiles | App-owned provider UserDataFolder; no per-view profile API exposed | Static API28 data-directory suffix, before first WebView in the process | Advanced lifetime/startup choice; no live per-view switch |
| Mixed content | No equivalent product TLS/certificate bypass option | API21 per-view setting plus app manifest/network cleartext policy | Existing NeverAllow/cleartext restrictions retained; no broad opt-out |

Source: [WebView2 navigation events](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/navigation-events),
[Android WebViewClient](https://developer.android.com/reference/android/webkit/WebViewClient)
and [Android WebView static settings](https://developer.android.com/reference/android/webkit/WebView).
Implementation details and executed qualification follow below.

## API, defaults and effects

WebView.AllowedNavigationSchemes is UI-thread state for one instance. The flags enum is
None=0, Http=1, Https=2, AboutBlank=4, Default=7. Default preserves absolute HTTP/HTTPS
and exact about:blank, including null Source/Navigate. No selection enables
file/content/data/javascript/custom URI commands. Static ValidateUri remains unchanged.
Existing IWebViewPeer/settings/request constructors are compatible; detection is an
additive optional IWebViewNavigationFilterPeer contract.

| Choice | Classification | Effect/risk |
| --- | --- | --- |
| Default | Default, compatible | Original URI policy and OS restrictions; HTTP can be cleartext where permitted |
| Https with AboutBlank | Explicit opt-in | Reject HTTP commands/reported top-level targets; ordinary TLS validation retained |
| Http re-enabled | Advanced, higher risk | Permit cleartext targets only where OS policy permits; never override manifest, mixed-content or certificates |
| None | Explicit opt-in, narrow scope | No URI/blank/local HTTPS loads; explicit raw HTML/reload still work and can contain network activity |

Raw HTML is a separate application operation, not an offline mode. Only its private
initial about/data URL is normalized; later redirects/user URI requests remain filtered.
Local catalogs additionally require Https and retain immutable CSP/collection-origin
restrictions. Default CSP bytes, untrusted events and IsOriginAuthenticated=false remain
unchanged; AuthenticatedMessaging is absent. No setting grants page-to-C# privileges.

A changed setting validates first, then uses canonical Stop/request/token/bridge
revocation. Loading stops without automatically unloading the visible page or reloading.
It causes no framework layout. Pending script/RPC operations are canceled; already-sent
effects cannot be undone. Equal assignment is a no-op. A new navigation/reload establishes
a ready document. The value survives recreation; forbidden queued/recreated URI requests
are not submitted. Reload returns false for forbidden URI/unsupported policy; history
targets are checked when reported by native hooks. No second lifecycle/history/focus system.

App URI violations throw ArgumentException before ID/token/native mutation; invalid bits
throw ArgumentOutOfRangeException. Reported violations precede public NavigationStarting,
revoke operations, restore the last logical committed request and raise
NavigationFailed(InvalidAddress). Physical DOM retention is engine-specific, especially
after Android POST started. Cancellation/failure observers may submit a newer allowed
request; older work cannot stop/overwrite/settle it. A throwing cancellation observer
still stops its old flow. Runtime-only property serialization is hidden from Designer.

## Native capability and unsupported behavior

| Platform | Actual detection | Scope |
| --- | --- | --- |
| Windows | Active Core whose NavigationStarting subscription succeeded before Initialized | App commands and reported top-level callbacks/redirects |
| Android API24+ | Active View/installed NavigationClient plus main-frame request metadata | GET override/reported redirects; POST/some paths use OnPageStarted after network start |
| Android API23 | Capability absent: legacy string callback lacks main-frame metadata | Default preserved; new opt-in explicitly unsupported |
| Older/other adapters | Optional interface absent/false | Default compatible; no inferred opt-in support |

Check BrowserCapabilities.NavigationSchemeFiltering after readiness. It is absent before
initialization/after retirement. Detection describes installed hooks, not qualification
of all providers. A ready changed nondefault setter throws NotSupportedException without
mutation. A setting configured before initialization is retained: unsupported queued
loads fail Engine without native submission; later allowed loads/raw HTML throw
NotSupportedException and Reload/history return false. Forbidden URI validation occurs
first. No automatic fallback; explicitly selecting Default restores baseline operations.

This is not a sandbox/network firewall. It cannot guarantee no outgoing POST, iframe,
image/fetch/worker traffic, or identify the original initiating caller. Android
AddJavascriptInterface remains untrusted. API23 unsupported detection is not a runtime PASS.

## Examples

    // After initialization, on the owning UI thread:
    if (!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.NavigationSchemeFiltering))
        throw new NotSupportedException("This app requires reported-main-frame filtering.");
    browser.AllowedNavigationSchemes = WebViewNavigationSchemes.Https | WebViewNavigationSchemes.AboutBlank;
    browser.Navigate(new Uri("https://example.com/"));

    // Advanced, higher risk: deliberate HTTP permission, still subject to OS rules.
    browser.AllowedNavigationSchemes |= WebViewNavigationSchemes.Http;

Alternatively configure before hosting and handle the explicit Engine failure for an
unsupported queued request. Do not silently fall back if filtering is an app requirement.
No TLS/certificate bypass or global engine/profile setting is exposed.

## Qualification scope

Deterministic tests cover all eight subsets, invalid flags, null/blank/static API,
independent instances, unchanged default/CSP suites, old optional peers, unsupported
queued/ready loads, raw/local separation, redirects, Stop/reload/recreation/disposal,
stale callbacks, untrusted messages and cancellation callback reentrancy. Cancellation
tests await asynchronous task settlement rather than assume an immediate IsCanceled bit.

Native fixtures positively execute main/same-origin/opaque iframe actors before attempting
a forbidden top-level HTTPS target; HTTPS is excluded so unchanged Android cleartext
policy cannot mask the callback check. App HTTP rejection, None/raw reload and HTTPS
catalogs are separate controls. Windows adds actual owned HTTP302 -> forbidden HTTPS
and cross-window recreation/capability/RPC recovery. Both samples exercise actual POST
denial; Android's fallback does not prove the server received no POST. No privileged
handler is registered and production CSP is not relaxed for these raw fixtures.

Android cross-scheme HTTP302: NOT RUN (sample cleartext=false); no certificate/manifest
weakening. Deterministic redirect coverage is not complete native/provider qualification.
API23/physical devices, Android product Activity recreation, foreground keyboard/manual
visual, full IME/DPI/accessibility/Designer/heap/trim/AOT remain NOT RUN. The validator
contains an explicit API23 unsupported-control branch which has not been executed.
See next-phase-delivery-report.md for final exact-head results/CI/evidence.
