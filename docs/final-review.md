# Final baseline review — 2026-10-10

Historical baseline evidence below remains unchanged. Phase 2 adds an experimental
local-content API/capability, optional adapter contract and neutral response types;
their inventory, semantics and new execution results are in [local content](local-content.md)
and [phase2-progress](phase2-progress.md). PR #9 is now merged, with no release selected.

This review covers the complete first implementation before its single baseline commit.
It supersedes the qualification summary in [the historical implementation report](baseline-report.md).
The feature remains **Development / Experimental**, unpublished and subject to maintainer review.
Local native execution, deterministic tests and hosted CI are separate evidence categories.
Publication metadata and hosted CI results are recorded in the PR and final delivery report;
they cannot be established before the commit containing this document exists.

## Scope and revision audit

WebView base and origin/main at review: `298ede4e1d76a5a2400dd7f3ec0cd3d7df44cfc3`.
The feature branch is `work/webview-baseline`. The exact main framework source dependency is
`69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`, the merge delivering NativeViewHost (#60 / PR #197).
The current framework master equals this pin; the Debug/Release builds therefore also check
current-master compatibility. No floating dependency update was needed.

All 55 implementation files were reviewed, including source, samples, tests, bootstrap,
project files, workflow and documentation. This report adds one file: the final manifest has
56 files. The original main framework checkout and its existing untracked configuration were
left untouched by this task. All builds use the isolated dependency checkout. The original
checkout was externally switched from its earlier documentation branch to master and
fast-forwarded during the review; its final tracked state is clean at the same dependency pin,
with the pre-existing untracked configuration preserved. No framework contract blocker was found.

## Blockers resolved during review

| Finding | Resolution and evidence |
| --- | --- |
| Non-number bridge document tokens could throw during JSON parsing; Android null input could throw. | Both transports validate token/payload shape and discard malformed input. Real native fixtures send malformed packets followed by a valid message. |
| Windows parse exception handling could swallow exceptions thrown by application message observers. | Observer delivery runs outside the transport parsing catch. Existing UI-thread exception policy remains in charge. |
| Raw HTML normalization could hide a reported forbidden redirect. | Only the initial raw HTML engine marker is normalized; redirects validate the actual URI. Four deterministic scheme regressions cover file/data/javascript/custom destinations. |
| Android POST fallback could retain a completed request ID or stop a newer request from a reentrant handler. | A new document gets a fresh ID; fallback Stop is conditional on the captured ID and View still being current. Native POST and reentrant replacement fixtures cover the path. This remains a post-start fallback, not a network sandbox. |
| Android navigation/history notifications could overwrite a newer reentrant programmatic request. | Current request checks occur after content replacement and observer delivery. |
| A cancellation registration exception could skip native peer disposal. | Callback revocation precedes cancellation; nested finally blocks guarantee peer disposal and state notification. A deliberately throwing provider registration regression verifies disposal exactly once. |
| Windows Starting acknowledgement could clear a newer command submitted by its handler. | Acknowledge the captured original command ID only. Stale acknowledgement regression and a real native POST reentrancy fixture passed. |
| Android provider registration could resolve a dispatcher before backend initialization. | It checks the initialized canonical backend first, matching Windows. |
| Ordinary Android sample runs emitted validation lifecycle logs. | Test lifecycle logs now require the explicit validate Intent extra. |
| Android CI implicitly depended on the hosted JDK default. | Select Temurin JDK 21 explicitly before installing/building .NET 10 Android. |
| Documentation promised null for every nonserializable script result. | Native circular-object projection differs: reviewed WebView2 returns null; Android WebView 113 returns {"self":null}. XML/API docs and runtime fixtures now state this explicitly. Native execution is preserved without an eval wrapper or scope/CSP changes. |

No existing published WebView API was renamed or removed. SourceAddress's existing null-as-blank
setter behavior now has AllowNull metadata and documentation. IsRedirect documentation reflects
Android's limited ongoing-load classification instead of claiming a definitive native redirect flag.

## Public API inventory and decisions

Every entry remains experimental until a release decision. **Candidate** means its basic neutral
shape is a reasonable stability candidate; it is not a stability promise. **Risk** means semantics
or an extension boundary require special attention before a public package. Synthesized record
constructors, properties, equality, cloning and deconstruction belong to their record's decision.
Inherited Control/NativeViewHost APIs are unchanged. Private/internal adapters are not exported APIs.

| Exported type/member | Decision | Semantics and review concern |
| --- | --- | --- |
| WebView : NativeViewHost; WebView(); WebView(IWebViewPeerFactoryProvider) | Candidate | Safe constructor, lazy provider resolution; explicit provider is borrowed and never disposed by the control. |
| Source : Uri? | Candidate | Null requests blank; raw HTML reports null; latest request, redirect/completion updates, same-URI assignment no-op. |
| SourceAddress : string (AllowNull setter) | Risk | Existing Designer string surrogate; null/empty requests blank, maps to Source. Keep until the canonical Designer supports Uri safely. |
| CanGoBack, CanGoForward, IsLoading, IsInitialized | Candidate | Read-only runtime state; not Designer-serialized. History/availability are per current native browser. |
| InitializationState | Candidate | Browser readiness independently of native hosting; hidden runtime property. |
| BrowserCapabilities | Risk | Ready operation surface; JS still requires opt-in. NavigationCancellation does not advertise a sandbox or complete interception. |
| IsJavaScriptEnabled | Candidate | Default false, UI thread; disabling cancels pending operations. Reload after changing bridge settings. |
| UserAgent | Candidate | Null default, no empty/control characters, future requests without implicit reload. |
| Navigate(Uri?), NavigateToHtml(string) | Candidate | UI thread; repeated URI supported, raw HTML has no supplied base URI. |
| GoBack(), GoForward(), Reload() : bool | Candidate | False while unavailable/loading. Fresh logical request; native history semantics are engine dependent. |
| Stop() | Candidate | Invalidates old document work, settles loading; no-op engine command while detached. |
| ExecuteScriptAsync(string, CancellationToken) | Risk | Native raw JSON, JSON-compatible values portable; cyclic/non-JSON projections engine dependent. Cancellation cannot undo effects already submitted. |
| PostMessageAsync(string, CancellationToken) | Risk | String submission, not receipt or authorization. Page acknowledgement required for delivery evidence. |
| GetDiagnostics() | Candidate | Detached immutable snapshot; read control on its UI thread, then consume snapshot elsewhere. CurrentSource requires redaction before logging. |
| ValidateUri(Uri?) | Candidate | Pure policy check, no runtime initialization; HTTP/HTTPS/exact about:blank/null only. |
| Dispose(bool), protected override | Candidate | Explicit UI-thread lifetime; derives must call base to retire owned native leases. No finalizer contract. |
| NavigationStarting | Risk | Cancelable, reentrant; Android POST notification may be after network start. Not all engine/frame paths intercepted. |
| NavigationCompleted, NavigationFailed | Candidate | Original request ID, at most one terminal event; loading settles after observer delivery in finally. |
| InitializationStateChanged, InitializationFailed, StateChanged | Candidate | UI-thread state notifications; browser failure is separate from host availability. |
| WebMessageReceived | Risk | Untrusted string, current-document lifetime guard; informational Source is not authenticated origin. |
| WebViewCapabilities: None, UriNavigation, RawHtml, History, Scripting, Messaging, NavigationCancellation, UserAgent | Risk | Flags describe operations and documented limitations, not platform parity or trusted capabilities. |
| WebViewInitializationState: Detached, Initializing, Ready, Failed, Disposed | Candidate | Fresh presentation can retry failed initialization; no timer retry loop. |
| WebViewErrorCategory: Unknown, Network, Certificate, HostNotFound, Connection, Canceled, InvalidAddress, Engine | Candidate | Neutral categories, no native enum/exception payload. |
| WebViewOperationException(category); Category | Candidate | Fixed safe message, no native inner exception. |
| WebViewNavigationRequest(Id, Uri, Html) | Risk | Adapter contract; monotonic original identity, exactly URI or HTML branch; not a native request ID. |
| WebViewSettings(IsJavaScriptEnabled, UserAgent) | Candidate | Immutable validated settings supplied by the control, UI-thread application. |
| WebViewDiagnostics(Platform, Engine, EngineVersion, InitializationState, CurrentSource, IsLoading, CanGoBack, CanGoForward, LastFailure, PeerGeneration) | Candidate | Detached values only; version may be unknown, generation local, source can contain private data. |
| IWebViewPeerFactoryProvider.CreatePeer(INativeViewSite, IWebViewPeerClient) | Risk | Existing canonical site contract, fresh uninitialized feature lease; host owns disposal. |
| IWebViewPeer : INativeViewPeer; Initialize, ApplySettings, Navigate, GoBack, GoForward, Reload, Stop, ExecuteScriptAsync, PostMessageAsync | Risk | Additive feature operations over the one canonical native peer contract. UI-thread ownership and stale original identity discipline required of third-party adapters. |
| IWebViewPeerClient.IsCurrent, Initialized, InitializationFailed, NavigationStarting, BeginNativeNavigation, NavigationCompleted, NavigationFailed, HistoryChanged, WebMessageReceived | Risk | UI-thread callbacks, current lease/request guards; no arbitrary native objects or service registry access. |
| WebViewNavigationStartingEventArgs(requestId, uri, isRedirect); RequestId, Uri, IsRedirect, Cancel | Risk | Original request identity; adapter redirect classification; Cancel not a network security boundary. |
| WebViewNavigationCompletedEventArgs(requestId, uri, httpStatusCode); RequestId, Uri, HttpStatusCode | Candidate | Final URI, null raw HTML, nullable status; Android supplies no reliable success status. |
| WebViewNavigationFailedEventArgs(requestId, uri, category); RequestId, Uri, Category | Candidate | Neutral top-level failure, not subresource failure. |
| WebViewInitializationFailedEventArgs(category); Category | Candidate | Isolated initialization failure, no profile path/native exception. |
| WebViewMessageReceivedEventArgs(message, source); Message, Source | Risk | String untrusted; Android Source null and bridge exposed to frames. |
| WindowsWebViewOptions(); UserDataFolder init | Risk | Platform-only persistent app profile policy; not shared API or automatic per-control isolation. |
| WindowsWebViewProvider(options); Register(options); CreatePeer(site, client) | Candidate | Platform startup adapter; lazy app environment, no controller ownership/cache. Register after backend initialization. |
| AndroidWebViewProvider(); Register(); CreatePeer(site, client) | Candidate | Platform startup adapter; no retained Activity/View, fresh site Context every lease. |
| AndroidWebViewPolicy.CreateMessageScript(string), MapError(int) | Risk | Public platform policy helpers, also available in pure net10.0 target; not main shared API or origin authorization. |

XML documentation, primitive defaults/Designer metadata and examples were inspected. The
deterministic signature audit walks exported shared types, member/constructor/event/generic
signatures and rejects native engines, backend types and native handles. Reflection is confined
to tests; production uses no reflection, private framework access or InternalsVisibleTo.

## Security and lifecycle assessment

The application owns one Windows provider/lazy environment; each current peer owns its controller.
All asynchronous initialization steps recheck canonical site/client currency; late controllers close.
Deferred focus requires the current site to still be focused. The sample's failure fixture uses
a temporary supported process-local missing fixed-runtime override, restored in finally, avoiding
the Edge-owned invalid-profile modal shown during the initial implementation attempt.

Android uses a fresh site Context and View/clients/bridge. Cleanup revokes browser generation,
cancels pending evaluations, removes the interface, detaches clients/content, stops, destroys and
disposes. JNI callback wrappers can remain alive until Chromium returns; canceled tasks cannot
update a retired control. No global cookie/cache purge or Activity/View migration is introduced.
Interrupted overlapping programmatic navigation replaces content in the same canonical site and
resets that browser's history; completed sequential navigation preserves native history.

JavaScript defaults off. Both message directions carry strings with safe escaping. Incoming
document tokens guard lifetime only; they are spoofable and do not authenticate pages/frames.
Android's minimal annotated interface has no reliable sender origin and delivers Source=null.
Permissions default deny, new windows suppressed, Windows downloads canceled, Android adds no
download saving/launch handler. Cookies/custom headers/permission UX/download manager/AndroidX
origin-restricted messaging remain deferred. File/content access and TLS are not weakened.

Canonical #60 owns placement, DPI/density, focus, validation, clipping, visibility, modal gating
and session recreation. No second host/focus/semantic tree was introduced. Rectangular placement,
ancestor scroll and MFN modal gating have native evidence; rounded/transformed clips, same-window
Skia overlays over native surfaces, broad accessibility and full IME are not promised.

## Final local validation

Final commands use the explicit pinned source root, GeneratePackageOnBuild=false, sequential
`-m:1 /p:UseSharedCompilation=false`, and the selected SDK 10.0.401. Actual machine paths and
binary/log artifacts remain outside this tracked report; portable commands are in [testing](testing.md).

| Check | Result |
| --- | --- |
| Solution restore | PASS |
| Final Debug solution build / tests | PASS; 0 build warnings/errors; 72/72 tests (45 shared, 15 Windows, 12 Android policy), 0 skipped |
| Final Release solution build / tests | PASS; 0 build warnings/errors; 72/72 tests, 0 skipped |
| Windows Debug / Release native --validate | PASS both, exit 0; test-profile cleanup completed, no matching test subprocesses remain |
| Android Debug / Release emulator validation | PASS both, RUNTIME PASS on API34 provider 113 |
| Activity rotation recreation / Home-resume | PASS native recreation/replay and resume; changed rotation settings restored |
| Debug/Release signed APK, Release AAB | PASS local signed packages, 0 build warnings/errors; APKs installed/executed, AAB built only, never installed/published |
| Public signature audit, Designer serialization/code generation/placeholder | Included in deterministic suite |
| Hosted CI | Must start after push/PR; recorded with exact head SHA in final delivery, not inferred from local checks |

Windows reviewed SDK 1.0.4258.31 / runtime 154.0.4258.62. Android reviewed emulator API34,
x86_64, density420, provider com.google.android.webview/113.0.5672.136; minSdk23 is only the
compile target. Native fixtures exercise ordinary JSON values, cyclic native result differences,
malformed bridge packets and then a valid packet, message escaping including quotes/backslashes/
newlines/Unicode/closing-script text, POST identities/reentrancy, cancellation, history, reload,
Stop, resize/visibility/ancestor scroll/clipping/modal gating and 50 new browser leases.
Windows additionally covers ten retirements during pending initialization and deferred focus.

Native automation is not human visual qualification. Actual native page input focus passed.
The final Android Release screenshot after recreation was inspected and shows actual offline
browser content with the MFN toolbar/status; this limited image check is not a full manual UI matrix.
Keyboard boundary remains PARTIAL because the foreground-dependent Windows repeat was not
reliably available and the complete Android MFN traversal loop was not qualified. Physical device,
API23 runtime, manual Visual Studio Designer, broad IME, multi-DPI and full accessibility are
NOT EXECUTED — environment/qualification unavailable. Fifty cycles are not retained managed/
Java/native heap proof or a long soak. Unique Windows test profiles are cleaned after loop exit;
the loopback server binds only loopback/random port and is disposed by the sample.

## Roadmap proposals and main #20 matrices

All actual WebView #1–#8 and main #20 remain OPEN. These are proposals, not issue changes.
#1 architecture, #2 external hosting prerequisite and #3 shared API are closure candidates after
maintainer acceptance. #4 Windows and #5 Android are implementation review candidates with
the explicit platform qualification limits. #6 baseline samples/tests implemented, broader
qualification remains open. #7 preview and #8 stable release are NOT STARTED.

| Main #20 phase | Item | State / remaining work |
| --- | --- | --- |
| 1 | Neutral WebView API / abstractions / capabilities | PASS baseline; experimental stability review remains |
| 1 | Windows / Android implementation | PASS baseline native scenarios; physical/API23 pending |
| 1 | URI/HTML, events/errors, back/forward/reload/stop | PASS baseline; SDK interception/history differences explicit |
| 1 | Lifecycle/disposal/resize | PARTIAL qualification; native scenarios present, retained heap/long soak/DPI/focus matrix pending |
| 2 | Scripts and bidirectional strings | PASS baseline; JSON-compatible portable values, Android origin limitation explicit |
| 2 | Filtering/cancellation and diagnostics | PASS baseline; cancellation is not complete network interception |
| 2 | Cookies / headers / permission hooks | PARTIAL; only permission denial, advanced APIs NOT IMPLEMENTED |
| 2 | Downloads / new windows | PARTIAL; safe suppression only, feature UX/APIs NOT IMPLEMENTED |
| 3 | Safe placeholder / serializer / code generation | PASS deterministic; manual VS NOT EXECUTED |
| 3 | ControlGallery | PARTIAL; dedicated Windows/Android feature samples, main Gallery untouched |
| 3 | Platform docs / contract tests / runtime checklist | PASS baseline with documented qualification gaps |

Recommended follow-ups (not created): supported packaged framework/bootstrap and preview decision
(existing #7), stable API/platform commitments (#8), origin-restricted Android messaging design,
POST/redirect interception and overlapping-history semantics, foreground keyboard/full focus/
accessibility/IME/multi-DPI, physical/API23/runtime-floor qualification, retained-Activity/native heap
profiling and long soak, manual VS Designer and main Gallery integration. These belong in later
scoped work and do not justify closing main #20 or implementing advanced Phase 2 in this PR.

## Complete source/documentation manifest

The 56 files below include 16 modified and 40 added files. Logs, screenshots, SDK/dependency
checkouts, obj/bin, test profiles, APK/AAB, secrets and local configuration are excluded.

```text
M .github/workflows/ci.yml
M .gitignore
M CONTRIBUTING.md
M Directory.Build.props
M Directory.Packages.props
M ModernFormsNext.WebView.slnx
M README.md
M ROADMAP.md
M docs/README.md
M global.json
M samples/ModernFormsNext.WebView.Sample.Android/ModernFormsNext.WebView.Sample.Android.csproj
M samples/ModernFormsNext.WebView.Sample.Windows/ModernFormsNext.WebView.Sample.Windows.csproj
M src/ModernFormsNext.WebView.Android/ModernFormsNext.WebView.Android.csproj
M src/ModernFormsNext.WebView.Windows/ModernFormsNext.WebView.Windows.csproj
M src/ModernFormsNext.WebView/ModernFormsNext.WebView.csproj
M tests/ModernFormsNext.WebView.Tests/ModernFormsNext.WebView.Tests.csproj
A Directory.Build.targets
A docs/android.md
A docs/architecture.md
A docs/baseline-report.md
A docs/capabilities.md
A docs/final-review.md
A docs/public-api.md
A docs/security.md
A docs/testing.md
A docs/windows.md
A samples/ModernFormsNext.WebView.Sample.Android/MainActivity.cs
A samples/ModernFormsNext.WebView.Sample.Android/Properties/AndroidManifest.xml
A samples/ModernFormsNext.WebView.Sample.Android/SampleApplication.cs
A samples/ModernFormsNext.WebView.Sample.Windows/LoopbackContentServer.cs
A samples/ModernFormsNext.WebView.Sample.Windows/NativeKeyboardValidation.cs
A samples/ModernFormsNext.WebView.Sample.Windows/Program.cs
A samples/Shared/NavigationRuntimeValidation.cs
A samples/Shared/RuntimeValidation.cs
A samples/Shared/SampleForm.cs
A samples/Shared/test-page.html
A scripts/bootstrap-framework.ps1
A src/ModernFormsNext.WebView.Android/AndroidWebViewPolicy.cs
A src/ModernFormsNext.WebView.Android/Platform/AndroidWebViewPeer.cs
A src/ModernFormsNext.WebView.Android/Platform/AndroidWebViewProvider.cs
A src/ModernFormsNext.WebView.Windows/NavigationCommandQueue.cs
A src/ModernFormsNext.WebView.Windows/WindowsWebViewPeer.cs
A src/ModernFormsNext.WebView.Windows/WindowsWebViewPolicy.cs
A src/ModernFormsNext.WebView.Windows/WindowsWebViewProvider.cs
A src/ModernFormsNext.WebView/WebView.cs
A src/ModernFormsNext.WebView/WebViewContracts.cs
A src/ModernFormsNext.WebView/WebViewEventArgs.cs
A tests/ModernFormsNext.WebView.Android.Tests/AndroidPolicyTests.cs
A tests/ModernFormsNext.WebView.Android.Tests/ModernFormsNext.WebView.Android.Tests.csproj
A tests/ModernFormsNext.WebView.Tests/DesignerGenerationTests.cs
A tests/ModernFormsNext.WebView.Tests/FakeProvider.cs
A tests/ModernFormsNext.WebView.Tests/GlobalUsings.cs
A tests/ModernFormsNext.WebView.Tests/WebViewTests.cs
A tests/ModernFormsNext.WebView.Windows.Tests/ErrorMappingTests.cs
A tests/ModernFormsNext.WebView.Windows.Tests/ModernFormsNext.WebView.Windows.Tests.csproj
A tests/ModernFormsNext.WebView.Windows.Tests/NavigationQueueTests.cs
```
