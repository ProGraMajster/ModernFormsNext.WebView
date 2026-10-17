# Phase 2 follow-up report — 2026-10-17

## Repository and delivery status

Repository: [ModernFormsNext.WebView](https://github.com/ProGraMajster/ModernFormsNext.WebView).
No main-framework edit, framework-pin change, package/release publication or version bump.
Main is `f4017e7be66eeebf235e9e296caf1e5e7855ad19`, containing normally merged
#36 → #37 → #38. Each merge has two parents; each following PR was retargeted/integrated
and its new head rechecked before merge. Local main was fast-forwarded after each.

| Stage | Delivery and conclusion | Local verification |
| --- | --- | --- |
| 1: #33/#11/#12 | #36/#37/#38 merged; scoped issues closed COMPLETED | Fresh full Debug/Release, 166/166 each; both native platforms PASS each |
| 2: #16 | [#39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39) OPEN; executable AndroidX research, privileged NO-GO | Product Debug/Release 166/166; both native platforms PASS; research APK and recreation PASS both |
| 3: #15 | [#40](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/40) OPEN; PARTIAL hardening, privileged NO-GO; issue stays OPEN | Full Debug/Release 188/188 each; both native platforms PASS each |
| 4: dependency audit | #17/#13/#18 blocked by privileged gate; #19 and #22 have independent bounded work | Live issue descriptions inspected; ROADMAP/DAG updated; no deferred handler/Blazor implementation |

This report is a current view. Earlier records in phase2-progress preserve historical
delivery states; references there to then-open #36–#38 do not describe current main.

## Stage 1 — audit and sequential merges

The [detailed merge report](pr36-38-final-audit.md) contains every exact reviewed head,
CI link and merge SHA, security/API/lifetime audit, test commands and limitations.
Normal merge commits are `8486ee30c8bff5c366ad37bc69e077efa9e9e741` (#36),
`98dc22f3306c99ea3cb5822a4271ef0748cf2d0f` (#37), and current main above (#38).
Final tree equals the originally qualified combined tree `91bf3dd7fbc7feafcdc04a7a50c23100d27b825d`.
Post-merge main [CI 39048806054](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39048806054)
is terminal SUCCESS for shared, Windows and Android. No new blocking source defect was found.

Fresh solution restore/build/test ran sequentially, SDK10.0.401, package generation off,
`-m:1 -p:UseSharedCompilation=false`. Each configuration: 0 warnings/errors, 139 shared,
15 Windows, 12 Android policy tests, no skips. Native Windows and Android included
resources, messaging security, transport, RPC, navigation/reentrancy and lifetime fixtures.

## Stage 2 — #16 AndroidX research

[ADR and detailed report](issue16-android-origin-research.md),
[runnable prototype/commands](../research/AndroidOriginProbe/README.md).
Implementation `7020ed70e41ef99254bc60e4c65b215d36e7f82c`; final PR #39 head
`96becb8a4692b9b9de0086c377b05521f6653d7c`. Terminal shared/Windows/Android SUCCESS:
[PR CI 39050287675](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050287675),
[push CI 39050284966](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050284966).
These exact-head results were verified before starting #15.

The isolated SDK APK selects research-only Xamarin.AndroidX.WebKit1.15.0.2 after archive/
merged-manifest checks rejected newer minSdk24 bindings for the API23 target. Ordinary
product Android resolves no AndroidX packages. The probe checks runtime feature support,
exact origin/port, direct frames, opaque/foreign frames, replies, redirects, reload,
listener replacement, Stop, old proxies and Activity recreation. It also executes the
existing production RPC JavaScript and Android escaping for a v1 data echo.

**NO-GO for privileged original-caller authorization:** a same-origin child can invoke
the parent's injected object and receive parent origin/main-frame metadata. No product
capability or handler was enabled. Forced-unsupported injection tests are not evidence
from a genuinely unsupported provider. AddJavascriptInterface remains unauthenticated.

Product full Debug/Release: 166/166 each, no skips, 0 warnings/errors, both native platforms
PASS both. Research APK builds and full probe/recreation PASS both. Release contains
Mono AOT output and uses partial trimming; no NativeAOT/full-trim claim. A real fixture
recreation crash exposed lazy JNI identity hashing after proxy disposal; materializing
managed-reference uniqueness before cleanup fixed the cause. Final probes require two
completions and old-fixture destruction; no timeout relaxation.

NOT EXECUTED: API23 runtime, physical device, genuine unsupported provider, full retained-
Activity/heap soak, process-death restoration, NativeAOT/full trimming. API34 success does
not establish API23 runtime compatibility; meta redirect is not an HTTP302 qualification.

## Stage 3 — #15 Windows hardening

[Detailed implementation/security decision](issue15-windows-message-hardening.md).
Existing Windows peer now binds private packets to a native-prepared per-request token,
checks controlled local native Source by parsed HTTPS origin, and revokes correlation
at document/cancel/failure/Stop/JS-disable/dispose boundaries. Completion cannot restore
retired identity. New tests cover stale-ID substitution, cross-peer replay, guessed and
malformed packets, origin spoofs, reentrancy and ordinary string compatibility. Public
helper/transport/RPC remain compatible; Android product and shared API are unchanged.

The same-origin parent-helper native counterexample still passes as **untrusted data**.
Thus #15 is PARTIAL/open; AuthenticatedMessaging remains absent, no services/handlers
are exported. This deliberately leaves a real security requirement unresolved rather
than presenting packet correlation as authentication. A stricter verified trust realm
or new execution-world research is required before privileged implementation.

Changed components: WindowsMessageGuard, WindowsWebViewPeer, linked Windows tests,
Windows sample validator/Program, plus security/capabilities/testing/platform docs and
this report/DAG. No new public/protected API, renderer/layout/focus service or dependency.
An initial test build's missing Xunit import was corrected. Final source review also
found a revoked-helper/JS-toggle race that could strand completion; completion now settles
without restoring the token, with a native Starting-observer regression. Both precede
final qualification.
Exact code/delivery SHAs, PR, final test and hosted CI results are recorded with delivery.

Implementation `4b748f2a29cac7090c870adbd7b6b7295d5a203c`; open
[PR #40](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/40) targets #39's
branch, keeping separate issue delivery. Full final restore and sequential Debug/Release
builds: 0 warnings/errors, **188/188 tests each** (139 shared, 37 Windows, 12 Android),
no skips. Windows and Android ordinary native suites RUNTIME PASS both configurations.
Both Windows executions include the corrected JS Starting race, captured old packet,
current-ID substitution and positive current fence. Android signed APK builds also have
0 warnings/errors. Logs: ignored `artifacts/issue15-final/`. Commands match Stage 1's
portable testing instructions, including explicit emulator serial and JDK21.

Subsequent delivery metadata changes documentation only. Exact final delivery head and
terminal hosted shared/Windows/Android checks are recorded on #40 after those checks
complete. Local native evidence qualifies the implementation separately from hosted
compilation/tests. No new PR is merged automatically.

## Stage 4 — inspected issues and next order

The live descriptions of #17, #13, #18, #19, #20 and #22 remain OPEN. No issue is closed
merely because prerequisites or one prototype exist.

| Issue | Current deliverable/gate | Recommended next step |
| --- | --- | --- |
| #17 Android secure bridge | BLOCKED: #16 parent-object relay invalidates proposed original-caller guarantee | Design a narrower verified trust realm or isolated-world experiment, API/provider fallback fail closed; retain API23 gate |
| #13 explicit C# registry | BLOCKED: RPC exists but no verified privileged platform transport | Wait; then explicit allowlisted scoped handlers with generated/AOT-safe JSON, no reflection/service locator |
| #18 Hybrid public control | BLOCKED: depends on #13/local assets/supported secure transport | API design only after trust and lifetime contracts; reuse existing NativeViewHost |
| #19 errors/cancellation | Existing unprivileged RPC covers typed errors/timeouts/cancellation/navigation; issue not fully accepted | First independent code follow-up: audit gaps and extend this canonical layer; privileged errors remain gated |
| #20 samples/diagnostics | BLOCKED for bidirectional Hybrid sample by #18/#19/security | Reuse current offline/data-only samples; no pretend handler example or payload/path leakage |
| #22 Blazor feasibility | Independent bounded research; NOT STARTED | Second independent follow-up: runnable public-API .NET10 Windows spike, license/assets/DI/dispatcher/AOT evidence and GO/NO-GO ADR |

Proposed order after maintainer review of the new PRs: #19 bounded unprivileged work;
targeted security-contract research for #15/#17 and independent #22 feasibility; only
after verified gates #13 → #18 → #20/#21. A positive #22 precedes #23/#24 and later full
Blazor implementation. Overlapping #28/#30 and #29/#31 need maintainer consolidation.

Ordinary WebView preview/stable #7/#8 follows its own packaging, API, platform qualification
and owner-approval gates. Optional Hybrid/Blazor work does not block that track.

## Evidence limits common to all stages

Pinned framework remains `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3` in an external source
checkout. Main framework's preexisting local configuration was preserved. Templates and
reference app were untouched, so template verification is not applicable.

Windows: WebView2 155.0.4283.60. Android: explicitly emulator-5554, API34 x86_64,
com.google.android.webview/113.0.5672.136. Physical attached devices were untouched.
Automated native logs do not establish human visual/UIA, foreground keyboard, full
IME/DPI/accessibility, all runtime floors or retained-heap leak freedom. Those remain
NOT EXECUTED. No security weakening, skipped tests, warning suppression, release or
package publication was used. New PRs stay open; only previously authorized #36–#38 merged.
