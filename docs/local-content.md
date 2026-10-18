# Experimental local application resources

`WebView.NavigateToLocalContent` serves an explicit immutable collection of **public**
application resources. It does not grant native privileges to the document. It uses the
existing `WebView : NativeViewHost`, provider and peer lifetime. No filesystem directory,
loopback listener, file URI, AndroidX dependency or new hosting system is introduced.

```csharp
using System.Text;
using ModernFormsNext.WebView;

var content = new WebViewLocalContent(new Dictionary<string, byte[]>
{
    ["index.html"] = Encoding.UTF8.GetBytes(
        "<!doctype html><link rel='stylesheet' href='app.css'><h1>Offline</h1>"),
    ["app.css"] = Encoding.UTF8.GetBytes("h1 { color: navy; }")
});
browser.NavigateToLocalContent(content); // UI thread; entry defaults to index.html
```

Only supply resources intended for browser access. Do not put secrets, configuration,
private documents or native command endpoints in the collection. Construction copies
all bytes; afterwards callers may release/change their input buffers. Limits are 1024
resources, 4 MiB per resource and 16 MiB total. This slice intentionally does not support
directory providers, asynchronous application callbacks, streaming large files or SPA fallback.

## Origin, paths and HTTP semantics

Each collection gets its own `https://<random>.mfn-app.invalid/` origin. Randomness isolates
collections, not users or authenticated senders. For requests delivered by the native
hook the entire reserved namespace is handled, including inactive collections, alternate
scheme/port/userinfo or trailing-dot hosts; the handler never falls back to the network.
This does not extend the native hook's documented redirect/worker coverage.

Resource keys are case-sensitive relative ASCII paths containing letters, digits, `-`,
`_`, `.`, and `/`. Empty/dot segments, trailing dots, leading/repeated slashes, backslashes,
percent escapes, query/fragment syntax, whitespace and non-ASCII characters are rejected.
No URL-decoding or filesystem normalization is performed. Validation operates on the
URL delivered by the engine; browsers can normalize `a/../b.html` to `b.html` before
interception. That can only reach explicitly public allowlisted bytes, never a private
file. Do not describe this as detecting every original traversal spelling in Chromium.

HTML, CSS, JavaScript, JSON and TXT use UTF-8. PNG, JPEG, GIF, WebP and ICO are binary.
MIME is selected by the case-sensitive extension and `nosniff` is always supplied.
SVG, arbitrary MIME/header overrides and redirects are unsupported. Exact-origin GET
returns 200 for a present key or empty 404 for an absent key; HEAD has an empty body.
Invalid paths return empty 400; other methods return empty 405. Inactive/retired content
returns empty 410 (foreign collection under an active mapping returns 404).
Android does not currently expose HTTP completion status in the shared event.
An empty error response can produce NavigationFailed rather than NavigationCompleted
(observed on WebView2 for 410), or an engine-generated error page (Android); applications
must handle both terminal events. Empty resolver bytes do not promise an empty browser DOM.

The resolver and immutable responses are safe on worker threads. Every response opens
a separate read-only, non-buffer-exposing stream owned by the native engine. An already
opened stream remains valid when a collection is replaced or a control is disposed;
no pending I/O, user callback or Activity is retained by a collection.

## Fixed document policy

This section describes the **unchanged default** policy. A catalog can now explicitly
select an immutable [typed policy](local-content-policy.md), including the Application
preset permitting only self connections and a self base URI in addition to the default
sources. It grants no native authority. Changing policy requires constructing/navigating
to a new catalog; Reload replays the selected policy. Unsupported older/custom adapters
reject nondefault policy explicitly rather than silently ignoring it.

All success/error responses carry CSP, `nosniff`, `Cache-Control: no-store`,
`Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` and
`Cross-Origin-Resource-Policy: same-origin`. No CORS permission is emitted.

```text
default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self';
connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none';
base-uri 'none'; form-action 'none'; frame-ancestors 'none'
```

In the default profile, inline scripts/styles, page eval, frame/worker resource requests,
forms, fetch/XHR/WebSockets and remote resources are outside this surface. Inherited
srcdoc/about:blank contexts are not a complete realm sandbox. Use separate local JS/CSS resources. JavaScript still
defaults off; enabling it is an explicit application decision. Native ExecuteScriptAsync
keeps its existing explicit engine semantics; CSP is not a restriction on trusted native
application code. Baseline strings remain untrusted, with no handler/RPC dispatch.

Native page navigation inside the collection retains its mapping. The shared policy
rejects reported main-frame departures from that origin; CSP supplies additional denial
for forms/frames. This is not complete network interception, particularly for Android
redirects. Explicit application `Navigate`/`NavigateToHtml` exits local mode. Cancellation
can leave the previous document visible with resource access revoked; Reload reissues
its logical local URI and reapplies the collection. History is engine-owned and only
the active collection is mapped; native navigation to another origin is rejected. An
explicit application Navigate to an old local URI clears the mapping and returns 410.

Stop invalidates existing document operations through the existing control contract.
Immutable loaded resources need no asynchronous cancellation. Canonical peer recreation
replays the collection and current URI; it does not retain DOM/history/JS state. Disposal
revokes native interception clients, releases subscriptions and drops collection references.

## Platform decision and capability

Windows uses `WebResourceRequested` with the three-argument filter and Document request
source kind (including frames/dedicated workers), then `CreateWebResourceResponse`.
It does not use a virtual directory mapping, which would expose filesystem/lifetime
semantics outside this bounded API. Workers are forbidden by CSP; global shared/service
worker interception is deliberately not registered per controller. An old runtime that
cannot install the required filter keeps baseline operations but advertises no LocalContent.
See Microsoft's [filter API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.addwebresourcerequestedfilter).

Android uses API21+ `ShouldInterceptRequest` and status/header-bearing
`WebResourceResponse`, available at minSdk23. Its interception client reads only a volatile
immutable snapshot; it never touches the canonical site, View, Activity or application
callbacks on Chromium's worker thread. Retirement clears the snapshot and marks that
client retired before destruction; replacement uses a fresh client. Android's hook does
not observe every redirect, blob or JavaScript URL, so this API returns no redirects and
does not claim a universal network sandbox. See Android's
[WebViewClient contract](https://developer.android.com/reference/android/webkit/WebViewClient#shouldInterceptRequest(android.webkit.WebView,%20android.webkit.WebResourceRequest)).
No AndroidX WebKit dependency is needed for static resources. Authenticated messaging
would need a separate decision and runtime feature detection; see the security matrix.

`BrowserCapabilities.LocalContent` is present only on a ready supporting peer. The
optional `IWebViewLocalContentPeer` preserves existing third-party baseline adapters.
A ready unsupported adapter throws NotSupportedException before changing logical state.
A queued request to an unsupported peer reports NavigationFailed(Engine) on readiness
without submitting it to the engine; Reload returns false for that retained unsupported
request. No framework pin/API change is required.

Both platform samples have a **Local resources** button and common native fixtures for
HTML/CSS/JS/binary loading, CSP inline/frame/connect denial, reload, replacement and
inactive origin denial. Qualification results and gaps are in [phase2-progress](phase2-progress.md).
