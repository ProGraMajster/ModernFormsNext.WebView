# Content and bridge security

Treat every remote page, HTML fragment, frame and incoming message as untrusted. JavaScript
defaults to disabled. Applications explicitly enable it before loading their controlled page;
ExecuteScriptAsync is an explicit capability with normal browser side effects. The sample
enables it to demonstrate the feature, not to establish a trust policy for arbitrary websites.

## Navigation and network policy

The shared API accepts absolute HTTP/HTTPS and exactly about:blank. Null requests blank.
file:, data:, javascript:, other about addresses and custom schemes are rejected as public
navigation targets. Raw HTML is a separate operation with no application-supplied base URI.
Only a raw HTML request's initial engine-private data/about:blank marker is normalized to logical
null Source; a reported redirect still validates its actual destination against the scheme policy.
No file access, directory mapping or filesystem bridge API is exposed. The separate
NavigateToLocalContent operation serves only explicitly public immutable bytes at a
reserved HTTPS origin; [local content](local-content.md) specifies path/MIME/CSP policy.
The [trust matrix](hybrid-security.md) keeps all privileged dispatch unavailable.
The [typed catalog policy](local-content-policy.md) permits explicit None/Self choices
without a raw CSP/header setter. Application policy changes only self connections/base
URI; it does not select a verified trusted realm, authenticate a script or grant native
operations. Default policy/JavaScript opt-in and all other production settings remain
unchanged. Native application ExecuteScript can bypass page CSP; do not use it as the
attacker when qualifying script/eval restrictions.
The [future trust strategy](hybrid-trust-strategy.md) defines unmet whole-realm/platform
acceptance gates; a local origin, immutable bytes, Source check or document token alone
must never enable native privileges. Untrusted scripts sharing a reachable main-world
object cannot be distinguished by the current channels.

NavigationStarting cancellation is a useful main-frame filter, not a sandbox. It does not
intercept all subresources, scripts, fetches, frames, POSTs or every native redirect path.
Android's SDK callbacks particularly do not provide complete POST interception or frame/origin
identity. Do not implement a network allowlist or confidentiality boundary using this event.
Do not embed sensitive application privileges in arbitrary remote content.

Android disables file/content access and both file cross-origin options, and requests
MixedContentHandling.NeverAllow. Its sample requests only INTERNET and leaves global cleartext
traffic disabled. Use HTTPS or raw HTML there. Windows uses the engine's browser policy; this
baseline does not advertise identical network/mixed-content enforcement. Windows runtime
validation alone uses a random-port HTTP loopback server with deterministic cleanup.
Certificate errors are never accepted automatically. No SSL error bypass or broad transport
exception is configured.

## String bridge

The supported page entry point is window.modernFormsWebView.postMessage(string). Native transport
envelopes carry the document's original request identity so delayed callbacks can be rejected.
That number is a lifetime guard, not authentication: hostile page code can inspect/spoof it.
Windows additionally correlates its private packet using a native per-document token and
checks native Source against the controlled local collection's HTTPS origin. This rejects
old-packet ID substitution; a current same-origin frame can still invoke the parent's helper.
Neither the token nor that origin check authenticates the original script caller. See the
[#15 decision and native tests](issue15-windows-message-hardening.md). Applications use the
documented helper, not private platform packet formats.
Malformed transport envelopes and invalid token/payload types are discarded. Incoming strings
have no trusted schema or privileges. Validate length, format and application
authorization before acting. No arbitrary native objects, reflection, filesystem, dispatcher,
service registry or application commands are exported to JavaScript.

Windows uses official WebView2 string messages, with host objects disabled. Android uses only
one explicitly annotated AddJavascriptInterface string method on API 23+, dispatching off-thread
callbacks onto MFN's UI thread. AddJavascriptInterface is visible to page frames and cannot
reliably establish caller origin. Android message Source is null; Windows Source is informational,
also not authentication. There is deliberately no shared property named Origin.

Enable the bridge only for content whose use your application authorizes. A subframe is not
made trusted by the top-level Source. The baseline has no AndroidX origin-restricted messaging
layer. If your threat model requires one, keep messaging disabled until that separate feature
is designed and reviewed. Do not expose secrets in bridge responses.

App-to-page messages are strings delivered to window message listeners. Android constructs the
JavaScript using JSON string serialization rather than interpolation of raw message text.
PostMessageAsync completion means submission, not delivery or authorization. Use a page-level
acknowledgement for delivery. Cancellation cannot undo native script side effects already started.

The optional [reusable transport](issue11-transport.md) preserves these rules. Its document
snapshot and cancellation token correlate lifetimes only; neither authenticates a caller.
It borrows the control, exposes no C# handlers and installs no JavaScript automatically.

The optional [RPC client](issue12-rpc.md) sends explicit requests to JavaScript methods
and accepts only bounded correlated responses. All results remain untrusted. It ignores
inbound requests and exports no C# handlers. A matching request/document ID never grants
permission; Android AddJavascriptInterface still does not authenticate the original frame.

## Native defaults and persistence

Permissions default to denied (Android includes geolocation and file chooser). No camera,
microphone or location permission is added to the sample manifest. New windows are suppressed;
they are not opened in external applications. Windows downloads are canceled. Android has no
download listener/service, so the adapter performs no automatic saving or external launch.
DevTools and native host-object exposure are disabled on Windows; Android remote debugging is
not enabled by this adapter. Browser debugging tools remain outside the baseline API.

Windows production profiles live under a per-application LocalAppData directory or an explicit
WindowsWebViewOptions.UserDataFolder. Profiles can retain cookies/cache even though no cookie API
exists. Android WebView data can be shared within the application process. Dispose releases the
control's resources and never globally clears cookies/cache. The adapter cannot provide profile
isolation or private browsing guarantees on Android. Diagnostics omit HTML, scripts, messages,
cookies, history and profile paths; CurrentSource can still be sensitive and requires redaction.
