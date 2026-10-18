# Phase 2 maturity report — 2026-10-18

## Outcome and delivery order

#39 then #40 were finally audited, freshly qualified for their exact heads and merged
with ordinary two-parent merge commits. Local main was updated after each. No blocker
was found in their bounded scope; neither supplies original-script authentication.
Issue #15 remains OPEN/PARTIAL and #16 retains its privileged NO-GO.

Three subsequent deliveries use separate branches and PRs. They remain open for review;
no automatic merge, issue closure, package/release publication or framework change.

| Stage | Branch / PR | Result |
| --- | --- | --- |
| 1, AndroidX #16 | [#39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39) | MERGED `9ebbfd9e059f0665cccbd94e4e574f74b9e89ce5`; original-caller NO-GO |
| 1, Windows #15 | [#40](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/40) | MERGED `b329da3eb114f9cd713b60feffe7242b22c0f0e1`; PARTIAL, issue stays open |
| 2, errors/cancellation #19 | work/issue-19-error-cancellation, [#41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41) | OPEN; real deadline/cancellation defects fixed; privileged portion blocked |
| 3, Blazor #22 | work/issue-22-blazor-feasibility, [#42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42) | OPEN; executable public .NET10 Windows prototype; interactive NO-GO under current content/security contract |
| 4, Hybrid #10/#15/#17 | work/issue-10-hybrid-trust-strategy, [#43](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/43) | OPEN; whole-realm/world proposal with measurable unmet gates; privileges remain disabled |

#41 is based on merged main. #42 is stacked on verified #41 for sequential review;
its research is independent of privileged handler work. The strategy follows verified
#42 in a separate branch. When reviewing, use each PR's base to see its own increment.
Full #23–#31 has not started. Baseline #7/#8 remains independent of Hybrid/Blazor.

## Exact commits and CI

| Delivery | Exact final reviewed/delivery head | Terminal hosted CI |
| --- | --- | --- |
| #39 | `96becb8a4692b9b9de0086c377b05521f6653d7c` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050287675), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050284966): all shared/Windows/Android SUCCESS |
| #40, after main integration | `577d3d45a10ea4718b04c411b47f29e6ebe74cbe` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053261390), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053258167): all SUCCESS |
| Main after #39 | `9ebbfd9e059f0665cccbd94e4e574f74b9e89ce5` | [Post-merge](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053252263): SUCCESS |
| Main after #40 | `b329da3eb114f9cd713b60feffe7242b22c0f0e1` | [Post-merge](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053637461): SUCCESS |
| #41 | `1facdc2974f2e060a3b32ba5bd6cfefacbc6a624`, implementation `68c97947f548910d335c07b6b69dc177af24c340` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124173180), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124170315): all SUCCESS |
| #42 | `17e3c8a4fbfc94563d25a2e88afc62ea6a8c16f4`, implementation `72a3aa0756ce689e342f1cb4189ffe61858d32dd` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39127365659), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39127362343): all SUCCESS |

Each preceding delivery was checked before the next stage. Strategy final head/jobs
are recorded in its PR after CI completes, avoiding a self-referential commit SHA.
Strategy implementation `2443c61` is delivered in open PR #43, based on verified #42.
Hosted shared/adapter tests, signed APK and research compilation are not native UI or
emulator execution. Windows hosted CI additionally runs actual Razor/public-manager
contract mode without native UI. The [merge audit](pr39-40-final-audit.md) has full
security/API/dependency/history details, tree equality and exact-head merge sequence.

## Real implementation and documentation

#19 changes existing WebView.RunOperationAsync and WebViewRpcClient mechanisms. Request
serialization previously fell outside the deadline; the existing deadline now includes
synchronous conversion and dispatcher delay. Monotonic checks prevent expired requests
from being posted and forward the remaining bounded budget. Cancellation is rechecked
after reentrant conversion and before success/error mapping, including coincident native
failure. Existing exception categories, protocol v1 and canonical document/peer tokens
remain authoritative. No second exception/cancellation layer or C# handler is added.

19 new deterministic cases include 10 red against the old implementation, then green.
Integration coverage exercises navigation, Stop, JS disable, host loss/recovery, delayed
responses and borrowed wrapper disposal. New shared RpcFailureRuntimeValidation runs in
both native samples; Windows additionally reparents a real pending RPC across windows
and recovers with the same client. [Detailed #19 report](issue19-errors-cancellation.md).

#22 adds only research/BlazorFeasibility.Windows plus research-only package versions and
Windows CI contract validation. SDK-compiled Razor/parameters, scoped DI/disposal and
public WebViewManager resource/navigation/root registration run for real. Native MFN
controls, existing WebView2 host, dispatcher, immutable resources and passive Razor DOM
work together. The official unmodified Blazor.start positively fails on connect-src;
base-uri denial and rejection of claimed native IPC are measured separately. No Blazor
page/privileged DI context attaches. Dependencies/notices are isolated from ordinary
WebView. [ADR, local publish diagnostics and commands](issue22-blazor-feasibility.md),
[runnable project](../research/BlazorFeasibility.Windows/README.md).

The separate [Hybrid strategy ADR](hybrid-trust-strategy.md) proposes immutable whole-app
trust, unprivileged remote views, platform qualification and revocation before document/
peer transitions. It distinguishes a trusted realm from an identified original script,
and covers actual AndroidX world APIs and Windows CDP as unqualified research alternatives.
It defines native attacker/control tests and explicit typed operation authorization;
no new runtime/public API, privileged registration or policy change is implemented.

ROADMAP/DAG, progress, security/trust matrix, capabilities, testing and docs index are
updated. Older #15/#16/follow-up reports are explicitly marked historical where PR state
changed. No template/main-framework edit or new public API stability promise.

## Local execution

| Stage/tree | Full Debug and Release | Local native lanes |
| --- | --- | --- |
| #39 exact head | 166/166 each; zero warnings/errors/skips | Product Windows/Android PASS both; research AndroidX full matrix + Activity recreation PASS both |
| #40 exact final head | 188/188 each; zero warnings/errors/skips | Product Windows/Android PASS both |
| #41 implementation/delivery | 207/207 each (158 shared/37 Windows/12 Android); zero warnings/errors/skips | Product Windows/Android PASS both, including new RPC failures and Windows host recovery |
| #42 implementation/delivery | Same 207/207 and zero warnings/errors/skips; research build/contracts PASS both | Product Windows/Android PASS both; Razor probe PASS both with intentional interactive NO-GO |
| Hybrid strategy, unchanged runtime sources | Fresh 207/207 each, zero warnings/errors/skips; research builds/contracts PASS both | Fresh product Windows/Android PASS both; AndroidX full matrix/Activity recreation PASS both; Razor native/profile cleanup PASS both with interactive NO-GO |

SDK10.0.401/runtime10.0.12, Windows WebView2 SDK1.0.4258.31: runtime155.0.4283.60
for #39/#40/#41, runtime155.0.4283.62 for #22 and strategy (both configurations),
Android API34 x86_64/provider com.google.android.webview113.0.5672.136, JDK21. Only
emulator-5554 was used; no physical phone was touched. API34 never qualifies API23 runtime.
Research AndroidX Release is Mono AOT/partial trimming; that is not NativeAOT/full trimming.

Commands: scripts/bootstrap-framework.ps1; solution restore; sequential Debug/Release
build with `-m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false
-p:EnableWindowsTargeting=true`, all using the external pinned ModernFormsNextSourceRoot;
solution tests `--no-build --no-restore -m:1`; Windows sample `--validate`; signed ordinary
Android APK, `adb -s emulator-5554 install -r`, explicit validation Intent and PID-filtered
terminal logs. Research APK: SignAndroidPackage and its Validate.ps1 -Recreate in both
configurations. Razor: build, `--validate-contracts` and its native Validate.ps1 in both.
Native validation is automated engine/host execution, not UIA or human visual acceptance.
Final native-log comparison corrected the #22 report's earlier runtime-version attribution;
the product initialization logs record the update between #19 and #22. No repeat run or
different source tree is substituted for the originally executed evidence.

#22 local self-contained win-x64 without trimming: zero warnings, published contracts and
native PASS. Default partial trim: 193 warning log lines, native initialization FAIL.
BuiltInComInteropSupport=true variation: 194 warning log lines, bounded contracts/native
PASS; unresolved reflection/COM/assembly warnings mean trimming stays UNQUALIFIED.
NativeAOT attempt: 135 warning log lines, then absent C++ platform linker; no binary/runtime
qualification. No warning suppression, source-pin change or package/release publication.

Ignored local evidence: artifacts/audit39, audit40, issue19, issue22, issue22-product,
hybrid-strategy-product, hybrid-strategy-origin and hybrid-strategy-blazor. Exact final
strategy SHA/CI snapshot is also written to artifacts/phase2-maturity-final-report.md
after completion. #19 Release crossed an overnight execution gap; its elapsed build
time is not a performance result. Final Release tests/native ran after resumption.

## Limits, cleanup and next gates

NOT EXECUTED: API23 runtime/physical devices/genuinely unsupported provider, foreground
keyboard/UIA/manual visual, interactive Designer, full IME/DPI/accessibility, retained
heap/Activity soak, product Activity recreation with in-flight RPC, interactive Blazor
events/JS interop/routing/hot reload/Android Blazor, execution-world security, full trim
or NativeAOT runtime. These are not inferred from a successful unit/native control test.
Cancellation cannot undo JavaScript already submitted to a native engine.

Final fresh Razor runs clean their own profiles after the whole owned process tree exits.
Two early OS Temp fixture directories remain because automatic approval review rejected
manual deletion with "blocked by policy" and no more specific reason; no alternative
deletion API was used. Two early failed artifact profiles remain as diagnostic evidence.
This is not a retained-heap leak qualification. No unrelated process/profile was removed.

The approved framework pin remains `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`.
The main-framework working copy advanced externally to `1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1`;
this task kept it read-only and preserved its pre-existing untracked .codex/config.toml.
The diff to the pin is empty for NativeViewHost, NativeViewHosting and both typed native
view sites. This is contract inspection, not a full compatibility build against that newer
framework commit. No framework revision is silently adopted.

Next work requires maintainer review of the open PRs and a separate executable security
experiment with an explicitly narrower trust claim. Keep #15 OPEN/PARTIAL and #17/#13/#18
blocked until native trust/channel/lifetime/authorization gates actually pass. Revisit #22
only with a reviewed content/security contract and fresh positive evidence; no full
#23–#31, stable API, package/version selection or release follows from these reports.
