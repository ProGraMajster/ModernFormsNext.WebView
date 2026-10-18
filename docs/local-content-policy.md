# Explicit local content policy ADR — #44/#45

## Decision

Extend the existing immutable WebViewLocalContent catalog with an immutable
WebViewLocalContentPolicy. Keep the old constructor and its exact default CSP. Add only
typed None/Self selectors for five existing CSP directives, with explicit application
resource opt-in. Do not add a global security switch, arbitrary CSP/header strings, URL
providers, a second host or native privileges. Future platform controls are tracked in
[#46](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/46); existing
#14/#32 remain the local-resource foundation, not duplicated issues/implementations.

The new policy changes **content loading restrictions**. Application means app-owned
public resources, not a verified whole-app realm or original-script identity. Existing
IsOriginAuthenticated remains false and AuthenticatedMessaging remains unavailable.
No handlers, DI, reflection, files or services are exposed by choosing any policy.

## API and activation

```csharp
using ModernFormsNext.WebView;

// Existing applications retain exactly the old behavior.
var restricted = new WebViewLocalContent(publicResources);

// Explicit opt-in for reviewed application bytes. Resources remain public.
var application = new WebViewLocalContent(publicResources,
    WebViewLocalContentPolicy.Application);
browser.IsJavaScriptEnabled = true; // Existing, separate UI-thread setting.
browser.NavigateToLocalContent(application);

// Granular restrictions; no custom CSP syntax or raw origin interpolation.
var withoutPageScriptsOrImages = new WebViewLocalContent(publicResources,
    new WebViewLocalContentPolicy(scripts: WebViewLocalContentSource.None,
        images: WebViewLocalContentSource.None));
```

WebViewLocalContentSource is an enum containing only None and Self. The sealed policy
constructor validates every value and exposes get-only Scripts, Styles, Images,
Connections and BaseUri. ContentSecurityPolicy is the actual generated, read-only header
for diagnostics. No raw CSP setter exists. Undefined enum values and null policy fail
at construction. Catalog construction retains its existing copying/count/path/MIME bounds.

WebViewLocalContent gains a two-argument constructor and a get-only Policy property.
The original one-argument constructor is preserved. Default and Application are shared
immutable values, not process settings. None/Self values are converted only to fixed CSP
tokens. Responses expose read-only headers; page markup/meta CSP cannot replace an HTTP
policy. A meta policy can further restrict loading, so applications must still test their HTML.

Select the policy before navigation. To change it, create a new catalog and navigate to
it: this obtains a new isolated origin and uses existing request/peer cancellation before
submission. There is no mutable policy update or delayed callback that can reapply it.
Reload/recreation replays the selected immutable catalog; Stop/JavaScript disable retain
their canonical cancellation behavior. An already open resource stream remains readable,
as documented by the existing public-byte contract; cancellation does not retract bytes.
One catalog used by two views shares immutable bytes/policy, not mutable per-view state.

## Implemented option/effect table

All five selectors support None/Self, not arbitrary hosts or paths. They apply to the
catalog's document response and remain subject to browser CSP semantics. Self refers to
that response's origin, not the top-level URL of an unrelated WebView.

| Setting | Default | Application preset | Effect / risk | Availability / activation |
| --- | --- | --- | --- | --- |
| Existing IsJavaScriptEnabled | false | unchanged | Enables page JavaScript and untrusted baseline strings; policy never enables it implicitly | Existing per-view setting; reload before endpoint reinstallation after a change |
| Scripts / script-src | Self | Self | None denies page scripts; Self permits reviewed catalog scripts. No inline/eval permission. Native app ExecuteScript is a separate explicit operation | ConfigurableLocalContent; new catalog/document |
| Styles / style-src | Self | Self | None denies page stylesheets; Self permits catalog CSS. Inline styling remains denied | Same |
| Images / img-src | Self | Self | None denies images; Self permits allowlisted catalog image bytes. No data/remote sources or arbitrary MIME | Same |
| Connections / connect-src | None | Self | Self permits CSP same-origin connections, including fetch/XHR and other mechanisms allowed by the engine's self semantics. It is not a fetch-only firewall. Resource resolver still serves only allowlisted GET/HEAD bytes | Same; deliberate increase in content access |
| BaseUri / base-uri | None | Self | Self permits a same-origin base element, including another path on that origin. It does not enforce one particular base path or authenticate content | Same; deliberate change to relative URL resolution |
| Frame/worker/object sources, forms, frame ancestors | None/DENY | unchanged | CSP denies those resource loads/submissions. Inherited srcdoc/about:blank contexts are not a complete frame/realm sandbox; tested inline actors cannot run under inherited script policy | Existing CSP/headers; not complete network interception or a privileged realm |
| Default sources, nosniff/no-store/no-referrer/CORP/no CORS | Existing restrictive values | unchanged | No wildcard/default resource access, MIME override, redirects, filesystem provider or CORS permission | Existing immutable resolver and response headers |

Default byte order/value remains:

```text
default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'
```

Application changes **only** connect-src and base-uri to 'self'. No TLS/certificate,
permission, frame, script-inline/eval or messaging-authentication bypass is introduced.
Supply only public resources; self fetch makes it easier for permitted page code to read
all allowlisted bytes. A compromised permitted script invalidates application-code trust.
Rejecting a foreign base does not promise a particular retained/fallback path: WebView2
155 falls back to the document URL, while Android provider113 retains the preceding valid
self base. Both reject foreign hosts/ports. The guarantee tested is foreign-base denial
plus actual same-origin base support, not caller authentication.

## Adapter capability and compatibility

Add optional IWebViewLocalContentPolicyPeer derived from IWebViewLocalContentPeer, with
SupportsLocalContentPolicy, and the additive ConfigurableLocalContent capability flag.
Existing third-party peers need not implement it. Default-equivalent policies still use
the old local-content contract. A nondefault policy requires both local interception and
the new declared policy support: a ready unsupported peer throws NotSupportedException
before state/identity/cancellation changes. A queued/replayed unsupported request fails
with Engine without native submission, and Reload returns false.

The native capability reports adapter support for delivering these validated policy
headers through its available HTTP interception API; it does not attest an engine's
universal sandbox or script identity. Windows sets support only after the required
Document request-source filter installs; unsupported runtimes keep it false. Android
uses status/header-capable interception (SDK API21+, product minSdk23) on its current peer.
Both paths must forward the catalog's headers without silent policy substitution. Genuine
CSP effects still require native provider/runtime qualification; there is no SDK query
that proves every CSP directive is enforced on every future/old engine.

Qualified runtime/provider rows and unexecuted floors are recorded after execution below.
Do not relabel API34 as API23. Old/custom adapter mismatch is explicitly tested without
claiming a run on a genuinely unsupported native provider. No product AndroidX or ASP.NET
dependency or framework-pin change.

## Limits and deferred options

| Area | Current behavior | Follow-up rule |
| --- | --- | --- |
| Navigation/schemes/origins/redirects | Existing HTTP/HTTPS/about:blank and local-origin main-frame policy | NavigationStarting is incomplete for POST/subresources/redirects; no universal network allowlist claim |
| New windows/downloads | Existing suppression/no automatic saving | Separate typed per-engine feature and native ownership/negative tests first |
| Camera/microphone/location/clipboard | Existing permission denial; no new API | Separate explicit operation/permission UX, feature detection and revocation |
| Cookies/storage/profile isolation | Existing engine persistence; Android can be process-shared | No hidden process-global mutation behind a per-view setting |
| DevTools/mixed content/TLS | Existing production defaults retained | No unrestricted certificate bypass; any advanced option must be individually designed/qualified |
| Privileged page → C# | Disabled | #15/#17/#13/#18 gates independent of CSP/assets; Source/nonce/request IDs never authorize it |

[W3C CSP](https://www.w3.org/TR/CSP3/) defines source-list/base/connect semantics;
the engine tests measure this implementation's actual effects. Native interception
limitations remain those documented in [local content](local-content.md) and
[Android WebViewClient](https://developer.android.com/reference/android/webkit/WebViewClient#shouldInterceptRequest(android.webkit.WebView,%20android.webkit.WebResourceRequest)).
Browser normalization may turn traversal spellings into another allowlisted public key;
the resolver does not promise to detect pre-normalized text or protect private filesystem
paths through a navigation callback. It exposes no filesystem in the first place.

## Qualification and delivery

Executed on SDK10.0.401, Windows WebView2 SDK1.0.4258.31/runtime155.0.4283.62,
and Android emulator-5554/API34 x86_64/provider113.0.5672.136, JDK21:

| Lane | Result |
| --- | --- |
| Full solution restore, sequential Debug/Release builds | PASS, zero warnings/errors |
| Full Debug/Release tests | 245/245 each, zero skipped: 196 shared, 37 Windows, 12 Android; 38 new policy cases |
| Windows native Debug/Release | RUNTIME PASS: all five selectors, actual fetch/base/denials, canonical lifecycle and application-policy cross-window peer replay |
| Android native Debug/Release | RUNTIME PASS: same policy/attack/control matrix, rejected foreign departure and reload/default recovery |
| Legacy/custom unsupported policy | Deterministic ready/queued/recreated-peer rejection PASS; not a genuinely unsupported native provider run |
| Original-script authentication / privileged IPC | Unavailable; no capability promotion or handler exposure |

Commands: pinned bootstrap/solution restore; full `dotnet build -c Debug/Release
--no-restore -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false`,
tests `--no-build --no-restore -m:1`, all with external pinned ModernFormsNextSourceRoot
and EnableWindowsTargeting=true; Windows sample --validate; Android signed APK/JDK21,
explicit `adb -s emulator-5554` install/validation Intent and PID-filtered terminal logs.
Ignored final evidence: artifacts/local-policy-qualified. Earlier diagnostic/red logs
are retained separately and do not count as successful qualification.

Fixture corrections retained the security criteria: page eval is tested from a loaded
catalog script instead of native ExecuteScript; normal/opaque HTML-parsed frame scripts
first execute as positive controls. Safe base-path differences between engines are
recorded above. A page navigation can cancel its own EvaluateJavascript callback before
Android returns it: the test still requires canonical-token cancellation, InvalidAddress
rejection, the retained local origin and successful reload. No policy, timeout, warning,
skip or threshold was relaxed to obtain PASS.

NOT EXECUTED: API23 runtime/physical device, genuinely unsupported native provider,
foreground keyboard/UIA/manual visual, Designer/full IME/DPI/accessibility/heap soak,
application-policy product Activity recreation, full trimming/NativeAOT runtime and any
privileged transport/realm/world. API34 is not evidence for API23. Native effects here
qualify the listed engines, not a universal network sandbox or future provider versions.

Delivered in [PR #47](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/47),
implementation `599c9e5`, based on normally merged main `b9a5c5e`. The final delivery HEAD
and terminal hosted CI are recorded on the PR independently of the local native evidence.
The owner subsequently authorized its final audit and normal merge ba7260a.
#44 API is accepted; #45 remains incomplete for outstanding native/manual qualification.
The separate #48 asset and #49 world experiments are also accepted research with privileged
IPC/interactivity NO-GO; no full #23–#31 is authorized by this ADR.
