# Baseline capability matrix

Implemented means the API/code path exists. Runtime qualification is separate: see
[testing](testing.md) and the [final review](final-review.md). BrowserCapabilities describes
the ready baseline operation surface; it does not claim a sandbox, identical engine behavior,
network policy parity or physical-device qualification.

| Feature | Shared contract | Windows WebView2 | Android SDK WebView |
| --- | --- | --- | --- |
| URI navigation | HTTP/HTTPS, about:blank | Implemented | Implemented; app network policy applies |
| Raw HTML | Null Source, no base option | NavigateToString; native size limit | LoadDataWithBaseURL(null); native history differs |
| Local public resources | Immutable bytes, isolated HTTPS origin, fixed CSP | Request interception with Document filter; capability false on unsupported runtime | API21+ ShouldInterceptRequest; worker-thread immutable snapshot, minSdk23 |
| Privileged messaging/RPC | Not implemented; fail closed | No authorized handler dispatch | No authorized handler dispatch; AddJavascriptInterface cannot authenticate origin |
| Baseline authentication metadata | IsOriginAuthenticated always false; original RequestId/PeerGeneration are lifetime correlation | Same-origin frames can invoke parent helper | Opaque frames can invoke annotated string bridge; Source=null |
| Private packet correlation | Does not authenticate an original script caller | Per-request native token; controlled local Source check; stale ID substitution rejected | Existing original-request string envelope, unchanged |
| Reusable transport | Optional borrowed wrapper, canonical context and cancellation | Existing native script/string path | Existing native script/string path; no origin authentication |
| Unprivileged app-to-JS RPC | Explicit v1 endpoint, typed metadata, bounded IDs/JSON/deadline/pending calls | Existing strings; feature-checked page JavaScript | Existing escaped strings; engine JS feature check, no origin authentication |
| Initialization | Detached/Initializing/Ready/Failed/Disposed | Async environment/controller | Fresh View on UI thread |
| Events | Main-frame Starting/Completed/Failed | Native NavigationId tracking | SDK callbacks; no native navigation ID |
| Cancel navigation | Cancel Starting; restores committed request | Native Cancel | Main-frame preflight/override; incomplete POST interception |
| HTTP status | Nullable completion status | Available when engine supplies it | Not supplied in baseline |
| Back/forward | Availability, false while loading | Native history | Native history; reset on overlapping programmatic navigation |
| Reload/Stop | Reload false while loading; Stop revokes pending work | Implemented | Implemented |
| JavaScript | Explicit opt-in; native JSON; cyclic/non-JSON projections differ | Official CoreWebView2 API | EvaluateJavascript |
| Page to app | Untrusted string; document lifetime guard | Official transport, informational Source | Minimal annotated bridge; no reliable origin/Source |
| App to page | Untrusted string; submission completion | PostWebMessageAsString | JSON-escaped MessageEvent script |
| JavaScript setting/UserAgent | Safe primitives; null agent restores default | Implemented | Implemented |
| Permissions | No request/approval API | Default deny | Default deny, geolocation/file chooser denied |
| New windows | No public API | Suppressed | Suppressed |
| Downloads | No public API | Canceled | No automatic download handler |
| Cookies/custom headers | Deferred | No shared API | No shared API |
| Native hosting | Framework #60 owns placement/lifetime | HWND site; controller content size | Android site + AndroidViewPeer attachment |
| Focus/clipping/scroll/modal | Framework hosting boundary | Native airspace limitations | Native airspace limitations |
| Session recreation | Replay latest URI/HTML; reset history | Cross-window fresh controller | Activity/presentation fresh View |
| Overlapping navigation | Latest logical request wins | Commands retain original IDs | Interrupted browser content replaced; history reset |
| Designer | Safe NativeViewHost placeholder | No engine required | No engine required |
| Diagnostics | Detached snapshot; source needs redaction | Engine/version | Provider package/version on API 26+ |

Session recreation is not full browser-state restoration: DOM, JS heap, native history, input,
selection and document scroll are not preserved. The framework owns geometry, clipping, DPI,
input, validation and modal gating; WebView adds no second implementation of these services.
Native browser surfaces keep the host's documented airspace restrictions. A Skia paint pass
does not capture native browser pixels. Nonrectangular/transformed clipping is not promised.

Future work requires separate design: origin-restricted Android messaging, complete request
interception, cookies/headers, permission UX, download/new-window policy, richer settings,
profile isolation and wider device/engine qualification. These are not silently enabled.

[#16 research](issue16-android-origin-research.md) adds a separate AndroidX SDK APK. It
verifies origin/direct-frame metadata and a parent-object relay counterexample. Privileged
authentication remains NO-GO; no product capability or AndroidX reference is added.

Local resource limits, HTTP/MIME/CSP and lifecycle semantics are in [local content](local-content.md).
Platform qualification and dependency gates are recorded in [phase2-progress](phase2-progress.md).

[#19 error/deadline hardening](issue19-errors-cancellation.md) includes JSON conversion in
the existing RPC deadline and gives requested cancellation priority over coincident errors.
This changes no authentication capability and exposes no C# handlers.

The optional [#22 research executable](issue22-blazor-feasibility.md) is outside product
projects. It demonstrates passive compiled Razor rendering and public manager seams;
interactive Blazor startup is NO-GO under current CSP/IPC contracts. Ordinary WebView
acquires no ASP.NET dependencies or Blazor capability from that experiment.

The [whole-realm/world strategy](hybrid-trust-strategy.md) is a proposed security contract,
not an implemented capability. Current authentication flags stay false; SDK/native frame
context, CSP and correlation are separate evidence from original-script authorization.
Separate views do not imply Android cookie/profile or process isolation.
