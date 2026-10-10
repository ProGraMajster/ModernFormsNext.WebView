# Implementation baseline review — 2026-10-10

> Historical snapshot from the initial implementation, before final stabilization and publication
> authorization. Counts and statuses below describe that earlier run. See [final review](final-review.md)
> for the current API decisions, fixes, qualification and manifest; PR metadata/hosted CI are in the delivery report.

This is a local, experimental implementation ready for code/API review. It is not a release
qualification, an approved public API, a remote CI result or authorization to close issues.
No commit, push, PR, merge, issue change, package/version selection or NuGet publication was made.
All feature changes are in the separate WebView worktree on `work/webview-baseline`.

## Audit and reproducibility

The clean WebView base was `298ede4e1d76a5a2400dd7f3ec0cd3d7df44cfc3` on main, matching
origin/main. It contained planning documentation, nonpackable project stubs and a skeleton
workflow; it had no executable WebView implementation. All roadmap issues #1–#8 were OPEN.
Main #60 was CLOSED through PR #197. Main #20, the complete WebView umbrella, remains OPEN.

The framework dependency is exactly `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`.
GitHub's current master was rechecked during final validation and equals this same SHA.
Therefore the pinned-contract and current-master builds qualify the same source revision;
they are not evidence for an additional, newer framework revision.

The user's original main checkout remains at `0fbb20a674effa41d463f18216311d9ce8b0b3c1`
on `docs/refresh-screenshots`, with its preexisting untracked `.codex/config.toml` untouched.
Compilation used an independent, clean dependency checkout, not that older original checkout.
Main source, contracts, sample applications, package metadata and configuration were not edited.

Public #60 factories/peers and typed Windows/Android sites are sufficient. The implementation
does not implement framework-private provider/session/callback contracts, use reflection or
copy native placement, focus, validation, clipping or host-lifetime infrastructure.
NativeViewHost and AndroidViewPeer retain their canonical responsibilities.

## Final execution evidence

| Check | Result |
| --- | --- |
| Solution restore, SDK 10.0.401 | PASS |
| Full solution Debug build | PASS — 0 warnings, 0 errors |
| Full solution Release build | PASS — 0 warnings, 0 errors; external framework references also built as Release |
| Full Debug tests | PASS — 37 shared + 14 Windows + 12 Android = 63; 0 failed, 0 skipped |
| Full Release tests | PASS — same 63; 0 failed, 0 skipped |
| Windows Debug native sample | RUNTIME PASS; exit 0; test-owned profile cleaned |
| Windows Release native sample | RUNTIME PASS; exit 0; test-owned profile cleaned |
| Windows engine | WebView2 Runtime 154.0.4258.62; SDK 1.0.4258.31 |
| Android Debug APK / emulator | PASS build/install; RUNTIME PASS |
| Android Release APK / emulator | PASS build/install; RUNTIME PASS after trimming/AOT |
| Android recreation Debug / Release | PASS — old Activity destroyed, peer detached, fresh Activity/peer ready, latest HTML replayed |
| Android Home / resume | Executed in Debug and Release; application resumed without a runtime failure |
| Android environment | Pixel_8 emulator, API 34, x86_64, density 420; com.google.android.webview/113.0.5672.136 |
| Physical Android / API 23 runtime | NOT EXECUTED — no physical device; API 23 environment unavailable |
| Hosted GitHub CI / Ubuntu runtime | NOT EXECUTED — no push/PR authorized; local commands executed on Windows |
| Visual checks | Android browser content, text field and MFN modal screenshots inspected; Windows real browser surface visible during execution |
| Visual Studio Designer integration | NOT EXECUTED — property metadata, detached paint, serialization/code generation tested independently |
| Main DemoApp / ControlGallery | NOT EXECUTED / unchanged — main repo is read-only; feature samples live in WebView repo |
| Heap/retained Activity profiling, long soak, broad IME/multi-DPI | NOT EXECUTED; no leak-free or full accessibility claim |

Commands and repeatable startup instructions are in [testing.md](testing.md). The executed
sequence was solution restore, sequential Debug build/test, sequential Release build/test,
Windows `dotnet run ... -c Debug/Release --no-build -- --validate`, Android
`dotnet build ... -c Debug/Release -t:SignAndroidPackage`, APK installation and validation intent,
and an additional local Release AAB build. All builds passed the explicit source root and
`GeneratePackageOnBuild=false`; solution builds/tests used `-m:1` and
`UseSharedCompilation=false`. A missing-source-root negative check failed with the intended
actionable diagnostic. The bootstrap script accepted the clean exact-pinned checkout.

Native scenarios exercise offline HTML, real JSON scripts, acknowledged messages in both
directions, canceled navigation, reload/Stop, two URI pages and history, resize/hide/show,
ancestor scrolling/rectangular clipping, MFN modal owner-input isolation, and 25 sequential
fresh browser leases. Windows additionally exercises cross-window replay/history reset and
isolated native initialization failure. These are actual engines, not the deterministic fake peer.

The original Windows failure fixture deliberately supplied a file as the profile directory.
Edge displayed its own modal error; an unattended run timed out. That fixture was removed.
The final fixture temporarily uses the SDK-supported process-local
`WEBVIEW2_BROWSER_EXECUTABLE_FOLDER` override pointing to a missing fixed-version directory.
It restores the previous value in finally and passed in Debug and Release without a dialog.
No installed runtime, registry, machine/user environment or production profile was changed.
See the [official environment creation contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2environment.createasync).

An earlier Windows native keyboard run passed actual Tab and Shift+Tab inside HTML and the
HTML-to-MFN boundary before the old failure fixture timed out. Later background-shell keyboard
runs failed the foreground-owner prerequisite; they did not inject input into another app.
The final harness exposes that desktop-dependent scenario explicitly as `--validate-keyboard`.
The final ordinary Debug/Release runs do not repeat keyboard injection. On the final Android
Debug APK, ADB Tab focused the HTML Send-to-app button and Shift+Tab returned to its editor.
Further Tab traversal reported the MFN Navigate button focused, while the native WebView root
also retained an accessibility focus flag. Full canonical boundary/accessibility-loop qualification
therefore remains PARTIAL, rather than treating that UI hierarchy alone as conclusive proof.

## Required 72-point report

| # | Topic | Implementation / evidence |
| --- | --- | --- |
| 1 | WebView base SHA | `298ede4e1d76a5a2400dd7f3ec0cd3d7df44cfc3`; separate worktree/branch, unstaged changes. |
| 2 | Main contract SHA | Exact #60 merge `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`. |
| 3 | Skeleton audit | Planning-only base; no browser, real samples or substantive test suites. Nonpackable policy preserved. |
| 4 | Roadmap audit | #1–#8 OPEN; main #60 CLOSED; #20 OPEN. No issue modified. |
| 5 | Build/bootstrap | SDK 10.0.201 baseline updated to 10.0.401; documented explicit source dependency; sequential builds. |
| 6 | Source-root/pin | Portable `ModernFormsNextSourceRoot`; bootstrap verifies exact SHA and clean checkout; CI exact checkout; no absolute ProjectReference. |
| 7 | Project/TFM layout | Shared net10.0; Windows net10.0-windows10.0.17763.0; Android net10.0-android/min23 plus pure-policy net10.0 target; two native samples, three suites. |
| 8 | WebView2 package | Official stable Microsoft.Web.WebView2 1.0.4258.31; Core/native loader only; unused WPF/WinForms wrappers removed at reference resolution. |
| 9 | Android dependencies | Android SDK WebView; no AndroidX WebKit, MAUI or Essentials. Sample HarfBuzzSharp 14.2.1.1 matches main's native asset override. |
| 10 | Registration | Explicit provider registration in existing PlatformServiceRegistry after backend bootstrap; lazy hosting resolution; no assembly scanning/module initializer. |
| 11 | Shared API | `WebView : NativeViewHost`; neutral state/events/settings/capabilities/diagnostics, documented feature provider/peer boundary. No native engine type leaks. |
| 12 | Source | Uri?; latest pending request; equal assignment no-op; Navigate repeats; redirect/completion update without recursion; cancellation restores committed source. |
| 13 | Raw HTML | NavigateToHtml; Source null; no base-URI overload; raw reload normalized; engine-native limits/history documented. |
| 14 | Initialization | Detached, Initializing, Ready, Failed, Disposed; browser readiness distinct from native attachment; no automatic retry loop. |
| 15 | Events/order | UI-thread Starting → loading → source/history → Completed/Failed → loading false in finally; original IDs and at-most-once terminal event; redirect counts may differ. |
| 16 | Filtering/cancel | Shared URI policy + cancelable Starting + main-frame native hooks; stale IDs rejected. Android POST/hook gaps explicitly documented; not a sandbox. |
| 17 | Error mapping | Stable Unknown/Network/Certificate/HostNotFound/Connection/Canceled/InvalidAddress/Engine; platform enums/exceptions not exposed; mapping suites passed. |
| 18 | History availability | CanGoBack/CanGoForward track current native history; no unavailable engine call; traversals return false during loading. Both real engines passed sequential history. |
| 19 | Reload/Stop | Reload requires current ready logical page and no loading; Stop settles state and cancels pending operations; obsolete callbacks ignored. |
| 20 | Windows async init | Environment/controller awaits return through MFN dispatcher; current-site/generation checks; late candidate closes; ready/failure observers outside native catch. |
| 21 | Environment ownership | Application provider owns one lazy shared environment task; no controller retained there; new lease may retry failed task. |
| 22 | Controller lifetime | Fresh per native lease; remove handlers/document bridge, close controller, release references; no feature finalizer. |
| 23 | User-data policy | Persistent LocalAppData app-directory hash by default; configurable application profile; production Dispose never deletes profiles; unique test profiles cleaned with bounded guarded deletion. |
| 24 | Android lifecycle | Fresh site Context/View; existing AndroidViewPeer attachment; revoke generations, remove bridge/clients, stop, detach, Destroy/Dispose; no global cache/cookie clearing. |
| 25 | Activity recreation | Debug/Release observed generation 1 → detached 2 → fresh ready 3, replay; later Release recreation also ready generation 5. No old View/context migration. |
| 26 | Cross-window reparent | Windows actual fresh peer, latest request replay, history reset; Android fresh presentation/site. Full browser session restoration is not provided. |
| 27 | JS execution | Explicit async call on UI thread, ready + opt-in required; linked lifetime cancellation covers navigation/Stop/retirement/disposal/disable/token. |
| 28 | JS result | JSON encoding of one value; string stays quoted; undefined/nonserializable native result null; native JS exceptions follow null-result semantics; transport uses neutral exception. |
| 29 | App→page | Windows string transport; Android JSON-escaped MessageEvent evaluation. Completion means submission; runtime checks require page acknowledgement. |
| 30 | Page→app | Minimal `modernFormsWebView.postMessage(string)` helper; original document identity envelope; active generation/request guard; UI-thread event. |
| 31 | Android bridge security | One annotated string method; all data untrusted; no sender/origin authentication, no arbitrary object bridge. Logical token only rejects stale callbacks. |
| 32 | JavaScript policy | Disabled by default; sample explicitly opts in. Disabling revokes work/removes or disables bridge; reload after settings changes for consistent installation. |
| 33 | Settings | IsJavaScriptEnabled and validated UserAgent; null UA restores engine default; no native settings object or speculative shared settings. |
| 34 | Permissions | Default deny Windows/Android; Android geolocation and file chooser denied; no neutral approval UI/hook yet. |
| 35 | New windows | Windows handled without opening; Android OnCreateWindow false; no automatic secondary browser. |
| 36 | Downloads | Windows cancel; Android no automatic download handler; controlled download API deferred. |
| 37 | Cookies/headers | NOT IMPLEMENTED shared API; existing engine state persists according to native policy; no global clearing on disposal. |
| 38 | Schemes | Public URI requests only HTTP/HTTPS/exact about:blank; file/data/content/javascript/custom rejected; raw HTML separate; Android file/content/mixed-content disabled. |
| 39 | Designer | Constructor/detached placeholder starts no engine; SourceAddress primitive surrogate; runtime metadata hidden; actual MFN serializer/code-generation round-trip and Skia placeholder paint passed. VS host untested. |
| 40 | Diagnostics | Immutable detached snapshot: platform, engine/version, initialization, source, loading/history, neutral failure, peer generation; no native handles/profiles/page payloads. Source needs application redaction. |
| 41 | Capability matrix | Ready operation flags, None before readiness; detailed shared/platform matrix in capabilities.md; no inferred platform/security parity. |
| 42 | Security docs | security.md covers untrusted content/bridge, native defaults, cancellation limits, TLS, profiles and deferred capabilities. |
| 43 | Windows sample | Real MFN Form, offline HTML, URL/history, scripts/messages and modal buttons; temporary validation profile and local loopback server; no main sample clutter. |
| 44 | Android sample | Application/AndroidWindowActivity startup, INTERNET only, cleartext false, backup false; same shared UI/offline resource and validation intent. |
| 45 | Shared tests | 37/37 Debug and Release; state/API/guards/reentrancy/lifetime/Designer coverage using legal fake feature interfaces and existing TestHost. |
| 46 | Windows tests | 14/14 Debug and Release; same internal queue/mapping source plus noninitializing provider constructor; no private reflection. |
| 47 | Windows real runtime | Final Debug/Release RUNTIME PASS, exit 0; isolated failure and cross-window included; no remaining test-owned runtime process. Keyboard repeated separately as described above. |
| 48 | WebView2 Runtime | Actual 154.0.4258.62. No runtime installation, update or bundling performed. |
| 49 | Android tests | 12/12 Debug and Release; pure error/escaping policy; not a substitute for JNI/native tests. |
| 50 | Android Debug | Current final APK installed; common native + HTTPS/history RUNTIME PASS; ordinary rotation/replay and Home/resume executed. |
| 51 | Android Release | Current trimmed/AOT APK installed; same RUNTIME PASS; rotation/replay and Home/resume executed. |
| 52 | Android provider | com.google.android.webview/113.0.5672.136 on API34 x86_64 density420 emulator; actual diagnostic, no inferred provider support. |
| 53 | Physical device | NOT EXECUTED — environment unavailable. |
| 54 | Leak/lifecycle | 25 real leases on each native runtime run; stale/disposal deterministic tests; Activity replacements observed; bounded pending registries. No retained-heap/profiler proof. |
| 55 | Focus/Tab | Native HTML input focus passed both; actual Android Tab/Shift+Tab passed; earlier Windows HTML and boundary keys passed. Final desktop repeat/full Android focus-accessibility loop PARTIAL. No copied focus manager. |
| 56 | Scroll/clipping | Real ancestor scroll changes native placement; rectangular clip intersection verified; native page scroll/content observed. Rounded/transformed clipping not promised. |
| 57 | Popup/modal | Native MFN modal owner input gating passed both; Android modal pixels inspected. Same-window Skia overlay over native browser remains unsupported airspace. Broader popup/z-order matrix untested. |
| 58 | Current-master compatibility | Current master equals pin; Debug/Release built that exact revision. Original older dirty main checkout never built/modified for this task. |
| 59 | CI changes | Three real shared/Windows/Android jobs, exact framework pin, SDK/workload setup, tests/sample/APK; no secrets, publication or local absolute path. Hosted execution not run. |
| 60 | Builds | Final restore/Debug/Release pass with 0 build warnings/errors; 63 tests per configuration. |
| 61 | APK/AAB | Signed local Debug/Release APKs built; both emulator-tested. Local Release AAB built separately; AAB not installed/published. No store signing/qualification claim. |
| 62 | Dependencies/packages | WebView2, test tools and sample HarfBuzz versions centrally pinned; SDK updated; existing IsPackable=false/GeneratePackageOnBuild=false preserved. No version/license/package-ID changes. |
| 63 | Files | Complete changed/new file manifest below; artifacts and dependency checkout ignored. |
| 64 | README/ROADMAP | Replaced planning-only status with experimental implemented baseline, setup/links, explicit review and release gates; no claim issues closed. |
| 65 | Issues #1–#8 | Proposed review states below; all actual issues remain OPEN. |
| 66 | #20 Phase 1 | Foundation matrix below; implementation PASS within documented limitations, broader lifecycle/platform qualification PARTIAL. |
| 67 | #20 Phase 2 | Scripts/messages/filtering/diagnostics PASS; cookies/headers not implemented, permission/download/window UX deferred. |
| 68 | #20 Phase 3 | Placeholder/serialization/docs/tests PASS; feature samples supplied; main ControlGallery/VS manual integration PARTIAL. |
| 69 | Follow-ups | API review; Android origin-aware bridge decision, interception/history semantics; manual boundary/IME/DPI/accessibility; physical/API23; heap soak; hosted CI; later packaging/release. |
| 70 | Security limits | String bridge does not authenticate content/origin; cancel hooks do not enforce network sandbox; native runtime must stay updated; default profile is not per-control isolation. |
| 71 | Breaking changes | No existing main API removed/changed; no existing WebView implementation to break. New experimental API requires review before public version commitment. |
| 72 | Review readiness | Ready for final local code/API review with explicit qualification limits. No commit/PR/release action yet authorized; not ready to claim stable or complete #20. |

## Proposed issue states — no remote changes

| Issue | Proposal after review |
| --- | --- |
| #1 Planning and architecture | Closure candidate once the ownership/API/security decisions are accepted. |
| #2 Native content hosting contract | Closure candidate referencing main #60/PR #197; prerequisite already delivered externally. |
| #3 Shared WebView API | Closure candidate after public API review of this experimental surface. |
| #4 Windows backend | Implementation review candidate; retain explicit keyboard/DPI/IME/runtime-floor qualification gates. |
| #5 Android backend | Implementation review candidate; record hook/history/origin limits and pending physical/API23/heap/focus qualification. |
| #6 Samples and tests | Baseline implemented; keep extended manual/platform qualification open. |
| #7 Preview NuGet | NOT STARTED; needs supported packaged framework dependency and explicit release approval. |
| #8 Stable release | NOT STARTED; API/platform/security/long-term qualification and release approval required. |

## Main #20 acceptance matrices

PASS refers to the implemented baseline with the listed evidence, not release completeness.

| Phase 1 item | State | Evidence / limit |
| --- | --- | --- |
| Shared neutral API | PASS | XML docs + 37 deterministic tests; new API pending maintainer review. |
| Backend abstraction/capability | PASS | Existing #60 hosting + neutral feature contract/ready flags. |
| Windows WebView2 | PASS | Real final Debug/Release scenarios. |
| Android native WebView | PASS | Real final API34 Debug/Release scenarios; physical/API23 untested. |
| URI + HTML | PASS | Both runtime paths, including sequential URI history and raw reload. |
| Events/error reporting | PASS | Deterministic ordering/cancel/reentrancy/mapping; native success/failure fixture Windows. Android native error matrix untested. |
| Back/forward/reload/stop | PASS | Both engines' real scenarios; concurrent Android navigation resets native history. |
| Lifecycle/disposal/resize | PARTIAL | Real resizing/25 leases/reparent/recreation/background; retained-Activity/heap soak and wider focus/DPI not qualified. |

| Phase 2 item | State | Evidence / limit |
| --- | --- | --- |
| JavaScript execution | PASS | Real JSON value on both, cancellation guards tested. |
| App↔page messages | PASS | Acknowledged real round trips on both; Android sender unauthenticated. |
| Navigation filtering/cancel | PASS | Deterministic state and real preflight cancel; documented Android hook gaps. |
| Cookies / headers / permissions | PARTIAL | Cookies/headers API NOT IMPLEMENTED; safe denial only, no neutral approval hook. |
| Download / new-window handling | PARTIAL | Safe suppression implemented; controlled feature APIs NOT IMPLEMENTED. |
| Developer diagnostics | PASS | Detached neutral engine/provider snapshots and capability docs. |

| Phase 3 item | State | Evidence / limit |
| --- | --- | --- |
| Designer placeholder | PASS | No engine in constructor/detached rendering; actual placeholder paint test. |
| Serialization/code generation | PASS | Existing MFN serializer/deserialize/generate path preserves safe primitives. VS designer runtime untested. |
| ControlGallery sample | PARTIAL | Equivalent dedicated Windows/Android feature samples supplied; main ControlGallery unchanged due read-only scope. |
| Windows/Android documentation | PASS | Startup, architecture, APIs, policies, testing, limitations and this evidence report. |
| Automated contracts/runtime checklist | PASS | 63 tests/configuration and actual two-engine Debug/Release execution; manual gates listed honestly. |

Do not use `Fixes ProGraMajster/ModernFormsNext#20` for this baseline. Phase 2 advanced scope
and broader platform/Designer qualification remain open.

## Changed and new files

The following manifest includes source/docs/configuration only. Build outputs, APK/AAB, browser
profiles, logs, screenshots, emulator data, dependency source checkout and `.codex/config.toml`
are not tracked. Original tracked files are marked `M`; newly added files are marked `A`.

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

## Remaining qualification and risk

The public surface is additive and experimental. Keep the native-host airspace restrictions,
UI-thread disposal and script/message document lifetime explicit. Android's platform callbacks
cannot provide complete pre-network cancellation or native navigation IDs; the interrupted
browser replacement/history-reset tradeoff must be reviewed. Remote helpers become available
at completion; callers must not assume early-page availability. Stop/canceled navigation revoke
the prior message helper until reload/new navigation. Android has no authenticated sender origin.

Required future evidence includes hosted CI/Ubuntu execution, physical/API23 and additional
provider versions, native input/focus/accessibility loops, IME, multi-DPI, wider popup/overlay
cases, and retained-heap/Activity profiling with a long soak. These are not silently converted
into PASS by successful builds, screenshots or 25 create/dispose cycles.

No template verification was needed: main startup/templates/DemoApp were unchanged. No change
to the accepted #60 contract is required by this implementation.
