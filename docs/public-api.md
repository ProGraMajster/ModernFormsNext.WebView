# Experimental public API

`WebView : NativeViewHost` exposes Source (Uri?), SourceAddress (string Designer adapter),
CanGoBack, CanGoForward, IsLoading, IsInitialized, InitializationState, BrowserCapabilities,
IsJavaScriptEnabled and UserAgent. Runtime state is hidden from Designer serialization.
GetDiagnostics returns an immutable snapshot, including engine/version, state, source,
loading/history, last neutral failure and lease generation. Source may contain sensitive
application data: diagnostics are not an invitation to log browsing addresses.

All mutating calls and event subscriptions/handling belong to the owning MFN UI thread.
Calls after disposal are rejected. Source/settings changes do not introduce hosting layout;
NativeViewHost remains the canonical control boundary.

## Source and HTML

- Before or during initialization, only the latest requested URI/HTML is retained.
- Same-URI Source assignment is a no-op. `Navigate(uri)` repeats intentionally.
- Source is the requested URI, then the final redirected/current URI after engine callbacks.
- A canceled Starting restores the last committed logical request; it emits no completion.
- Native user navigation receives a fresh logical identity. Redirects can emit more Starting
  callbacks; identical callback counts are not promised across platforms.
- URI policy accepts absolute http/https and exactly about:blank. Null Navigate requests blank.
- `NavigateToHtml(html)` has no base-URI option. Source is null. Relative network links have
  no application-specified network base. Supply absolute URLs when needed.
- Raw HTML reload is normalized to the same logical null Source even when the engine uses
  a private data/about address. Native history behavior for raw HTML differs by engine.
- Session recreation replays the latest URI or HTML, resets loading/history and initializes
  a fresh browser. It does not restore full history, DOM, form input, scroll position or JS heap.

`GoBack/GoForward/Reload` return false when unavailable or while loading. `Stop` settles IsLoading and rejects
late completion/history callbacks for the stopped identity. Scripts/messages submitted before
Stop are canceled; reload/navigate establishes a new document message identity.
Canceling Starting also revokes pending document operations and the previous helper's message
identity. The retained page remains visible, but reload it before resuming page-to-app messaging.
This conservative lifetime rule prevents an obsolete document callback from being accepted as
belonging to a newer request.

## Events and reentrancy

For local resources use `NavigateToLocalContent(WebViewLocalContent, entryPath = "index.html")`.
`BrowserCapabilities.LocalContent` detects ready native interception support. This reports
a real HTTPS Source and retains the collection for reload/recreation; local reload reissues
that URI to reapply a revoked mapping. `WebViewLocalContent` copies inputs, validates paths,
infers MIME and exposes GetUri/CreateResponse for deterministic adapters. Responses expose
immutable headers and fresh read-only streams. `IWebViewLocalContentPeer` is an optional
adapter contract preserving baseline adapters. See [local content](local-content.md) for
limits, CSP, lifetime, unsupported-peer behavior and the complete example.

NavigationStarting is cancelable and provides RequestId, Uri and IsRedirect. Windows uses the
native redirect flag; Android classifies ongoing load callbacks with limited SDK information.
No user/programmatic flag is exposed. For accepted requests the order is Starting -> loading=true
-> native navigation -> source/history -> Completed or Failed -> loading=false.
Completion/failure observers see loading=true until the finally block. Reentrant navigation
prevents that finally block from clearing the newer request's loading state. Terminal completion
is emitted at most once per logical identity. Subresource errors are not navigation failures.

NavigationCompleted supplies the final URI and optional HTTP status (Windows supplies it;
Android does not reliably supply a success status). NavigationFailed exposes only the stable
Unknown/Network/Certificate/HostNotFound/Connection/Canceled/InvalidAddress/Engine categories.
InitializationStateChanged and InitializationFailed distinguish browser readiness/failure from
native-host attachment. There is no timer-based retry loop; a fresh presentation/lease can retry.

## Scripting and messages

```csharp
browser.IsJavaScriptEnabled = true; // before loading the trusted application page
browser.NavigateToHtml("<h1>Hello</h1>");
// After NavigationCompleted:
string json = await browser.ExecuteScriptAsync("({ answer: 42 })", cancellationToken);
using var value = System.Text.Json.JsonDocument.Parse(json);
await browser.PostMessageAsync("Hello from the app", cancellationToken);
browser.WebMessageReceived += (_, e) => ValidateUntrustedMessage(e.Message);
```

ExecuteScriptAsync returns raw JSON representing one JavaScript result. Strings stay JSON
encoded; callers should parse them. Undefined maps to JSON null. JSON-compatible strings,
numbers, booleans, null, objects and arrays have portable value semantics. Cyclic and other
non-JSON results use the native engine's projection and are not portable: the reviewed WebView2
returned null for a cyclic object while Android WebView 113 returned {"self":null}. Return
JSON-compatible values for cross-platform application logic. The adapter does not add an eval
wrapper, change script scope or bypass page Content Security Policy to force a common serializer.
JSON encoding details (e.g. Unicode escapes versus quoted escapes) need not be byte-identical.
JavaScript runtime exceptions follow the native null-result semantics, distinct from transport
errors, which use WebViewOperationException. No synchronous script API is provided.

Before readiness or with JS disabled, scripting/messaging is rejected. Cancellation, navigation,
Stop, retirement and disposal settle outstanding tasks without waiting for obsolete native
callbacks. Native execution already submitted is not guaranteed to stop executing side effects.
CancellationTokenRegistration/linked token ownership is bounded and disposed.

`window.modernFormsWebView.postMessage(string)` sends an untrusted string to the app.
App messages reach `window.addEventListener('message', handler)`. PostMessageAsync completes
after submission, not after application-level delivery; use an explicit page acknowledgement
when delivery matters. Messages include an informational Source where available; Android reports
null because its bridge does not identify the calling frame/origin. No property is named Origin.
Both adapters install their helper for raw HTML at load and for remote pages at completion; remote
scripts must wait until it exists. JavaScript/bridge enablement should be configured before load;
after changing it, explicitly reload to establish consistent document bridge installation.

UserAgent=null selects the platform default. Overrides reject empty/control-character values,
apply before and after initialization, and affect future requests without automatically reloading.
Advanced native settings objects are never exposed in shared API.

## Designer

The inherited Skia placeholder says WebView and starts no browser/network operation.
Source is hidden because the current Designer deliberately does not edit Uri or execute converters.
SourceAddress serializes that logical URI through its supported string path; JS and UserAgent use
primitive properties. Raw HTML is a runtime operation, not Designer content serialization.
