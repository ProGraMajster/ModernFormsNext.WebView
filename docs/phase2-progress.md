# Phase 2 progress — 2026-10-18

## Maturity follow-up

#39 → #40 were finally audited, freshly qualified and normally merged into main
`b329da3eb114f9cd713b60feffe7242b22c0f0e1`; [post-merge CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053637461)
is SUCCESS. [Merge report](pr39-40-final-audit.md) has every SHA/CI/result. #15 stays
OPEN/PARTIAL; privileged messaging remains unavailable. #19 implements the
[error/deadline fixes](issue19-errors-cancellation.md) in a new branch/PR. Sections below
preserve historical states, including then-open #39/#40.

#19 final local Debug/Release: 207/207 tests each, 0 warnings/errors/skips;
Windows and Android native RUNTIME PASS both, including the new failure matrix and
Windows outstanding-RPC host recreation. Delivery/CI is tracked on its separate open PR.

#19 delivery: [PR #41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41),
implementation `68c97947f548910d335c07b6b69dc177af24c340`; issue remains open for review
and its privileged portion remains gated. No new PR is merged automatically.

#19 final delivery head `1facdc2974f2e060a3b32ba5bd6cfefacbc6a624` has terminal SUCCESS
in [PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124173180)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124170315),
verified before #22 implementation. [#22's executable prototype/ADR](issue22-blazor-feasibility.md)
tests public .NET10 APIs, compiled Razor/native controls, scoped lifetime and actual CSP
startup denial. Interactive Blazor remains NO-GO under the current contract; no #23–#31
implementation or privileged native IPC is enabled.

#22 delivery [PR #42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42),
implementation `72a3aa0`, final `17e3c8a4fbfc94563d25a2e88afc62ea6a8c16f4`: all shared/
Windows/Android jobs terminal SUCCESS in [PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39127365659)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39127362343),
verified before the [separate Hybrid strategy](hybrid-trust-strategy.md). That strategy
defines verifiable whole-realm/platform gates and isolated-world alternatives, without
promoting current channels or changing the original-caller NO-GO. Both candidates remain
unqualified. The [maturity report](phase2-maturity-report.md) supersedes historical
delivery states below; new #41/#42 and strategy PRs stay open.

## Current follow-up delivery

[The current detailed report](phase2-followup-report.md) supersedes historical state below:
#36/#37/#38 merged normally into main `f4017e7`; issues #33/#11/#12 completed. #16 research
is delivered in open #39, final head `96becb8a4692b9b9de0086c377b05521f6653d7c`, with all
three hosted jobs SUCCESS in [CI 39050287675](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050287675).
#15 adds Windows packet/document hardening in a separate dependent PR; the original-caller
security requirement remains PARTIAL/open. Both platform privileged decisions are NO-GO
for the tested same-origin parent-relay threat. #13/#17/#18 remain gated, while #19 can
extend unprivileged RPC independently and #22 can perform its bounded feasibility spike.
No handler/Hybrid/Blazor implementation, release or framework-pin change is implied.

#15 delivery: open [PR #40](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/40),
implementation `4b748f2a29cac7090c870adbd7b6b7295d5a203c`; final Debug/Release 188/188
each, zero skips/warnings/errors, Windows/Android native RUNTIME PASS both configurations.
The exact final delivery head and terminal hosted CI links are recorded on that PR.

## Subsequent audit and AndroidX research

#36 → #37 → #38 were audited and normally merged; local/remote main became
`f4017e7be66eeebf235e9e296caf1e5e7855ad19`, with terminal successful CI. Issues #33/#11/#12
are closed for their scoped criteria. [Merge audit](pr36-38-final-audit.md) records exact
heads/merges and fresh Debug/Release 166/166 plus Windows/Android native PASS both.
[#16](issue16-android-origin-research.md) now has an isolated AndroidX SDK prototype and a
privileged NO-GO decision from a real same-origin parent-object relay. #17/#13 remain gated.
The earlier delivery records below are historical, including references to then-open PRs.

#16 delivery: [PR #39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39),
implementation `7020ed7`; product regressions 166/166 Debug/Release, both native platforms
PASS; research APK and Activity recreation PASS both, privileged policy NO-GO.

## Sequential follow-up

PR #35 completed a final audit and normal merge `79eaff579755578ce5a1faf19c861ffba86719d1`.
Local main equals that merge; [post-merge CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39042236260)
is SUCCESS. [Final audit](pr35-final-audit.md) distinguishes fresh and prior evidence.
The historical first-delivery record below remains dated evidence, not the current merge state.

#33 now adds [native messaging security verification](issue33-security-validation.md),
immutable original callback metadata and an explicitly unavailable authenticated capability.
Debug/Release 121/121 and both native platforms PASS; privileged transport is not enabled.
The next stages #11/#12 remain separate branches/PRs and are started only after the
previous stage's exact-head checks pass. New PRs are intentionally left unmerged.

| Follow-up | Delivery | Local validation |
| --- | --- | --- |
| #33 | [PR #36](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/36), code `c001685` | Debug/Release 121/121; Windows/Android native PASS both; CI recorded on exact PR head |
| #11 | [PR #37](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/37), code `efccb3c` | Debug/Release 128/128; Windows/Android native PASS both; targets #36 branch |
| #12 | [PR #38](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/38), code `b9225fe` | Debug/Release 166/166; Windows/Android native PASS both; targets #37 branch |

#33 final head `5466e712ed7bd276420a8374c105f3a878293f39` has terminal SUCCESS
in [PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043213480)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043209196).
Only after that verification, #11 implemented the [reusable untrusted transport](issue11-transport.md).
Debug/Release: 128/128 tests, Windows/Android native PASS; no privileged handlers.
Its dependent PR targets the #33 branch. #11 final head
`9b03ae8d3b08e2d4f2d4196758dd31a4d8d38a39` has terminal SUCCESS in
[PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39044791926)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39044788970).
Only then #12 started on `work/issue-12-rpc`, based on that verified head.
It adds [bounded versioned RPC](issue12-rpc.md) for unprivileged app-to-JavaScript calls;
there is no inbound C# dispatch or authentication upgrade. Its PR targets #11's branch.
Its final local Debug/Release builds have 0 warnings/errors and 166/166 tests each;
Windows and Android emulator native suites PASS in both configurations. The stage
report distinguishes native evidence, hosted CI and unexecuted qualification lanes.

The sections below retain the historical first-delivery state of PR #35.

## Verified baseline and scope

WebView main was clean at `12a3beace771c4b694479d2e635c1851cf75c2d5`, containing merged
[PR #9](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/9).
Work branch: `feature/webview-phase2-secure-foundation`, from that exact origin/main.
There were no other open WebView PRs at audit. Main framework master was
`307f08be46708d83222c93aac1912f81cf77d79a`, with no open PRs at audit; its local untracked
configuration was preserved. Framework issues were inspected, including open umbrella #20.

The bootstrap pin remains `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`.
Diff inspection against current framework master found no changes in NativeViewHost,
NativeViewHosting, WindowsNativeViewSite or AndroidNativeViewSite. No framework change,
floating dependency, dependency update or unpinned runtime qualification was performed.
The bootstrap now defaults outside this repository to prevent inherited NuGet central
versions (the original nested checkout produced NU1008). Existing checkouts were preserved.

All GitHub WebView issues #1-#8 and #10-#34 were **OPEN** at audit; no #9 issue exists
because that number is the merged PR. Status below describes work, not GitHub closure.
DONE means a bounded implemented/documented deliverable; it never asserts a release.

| Issues | Work status | Evidence / remaining gate |
| --- | --- | --- |
| #1 | DONE baseline architecture | Merged baseline; new trust boundaries documented separately |
| #2 | DONE external contract | Main #60/#197; consumed unchanged, not reimplemented |
| #3 | DONE baseline implementation | Shared control, API/Designer/callback tests; public stability review separate |
| #4 | IN PROGRESS qualification | Windows code and native baseline; full focus/DPI/IME/heap matrix pending |
| #5 | IN PROGRESS qualification | Android code and emulator baseline; API23/physical qualification pending |
| #6 | IN PROGRESS qualification | Two native samples and three suites; extended manual qualification pending |
| #7, #8 | BLOCKED release | Official framework package, platform commitments, packaging and explicit owner approval; no Hybrid/Blazor dependency |
| #10 | IN PROGRESS | Architecture decision and trust matrix supplied; optional privileged API not approved/implemented |
| #11, #12 | NOT STARTED | Next code increment: reusable transport, bounded versioned RPC; no public skeleton added |
| #13 | BLOCKED | Requires RPC and authenticated platform transport before handler exposure |
| #14, #32 | IN PROGRESS | Useful immutable static-resource increment implemented; merge and broader platform qualification remain |
| #15 | BLOCKED | Requires #11/#12 and privileged native origin/frame/lifecycle tests |
| #16 | NOT STARTED implementation | Official APIs inspected; AndroidX feature/binding prototype and ADR still required |
| #17 | BLOCKED | Current AddJavascriptInterface cannot authenticate caller; requires verified #16 plus #12/#14/#33 |
| #18-#20 | BLOCKED | Depend on verified transport/RPC/handlers; ordinary resource sample is not a Hybrid sample |
| #21 | BLOCKED release | Separate platform qualification and explicit approval; no preview created |
| #22 | NOT STARTED | No Blazor feasibility experiment or GO is claimed |
| #23-#27 | BLOCKED | Requires positive #22 and subsequent host/assets/component prerequisites |
| #28, #30 | BLOCKED | Android Blazor feasibility; overlapping descriptions require maintainer consolidation |
| #29, #31 | BLOCKED | Qualified optional Blazor layer; overlapping package/sample descriptions |
| #33 | IN PROGRESS | Trust matrix, baseline source/ID non-authentication and stale tests, local native frame CSP; privileged hostile-frame/redirect matrix pending |
| #34 | DONE scoped DAG | ROADMAP contains separate baseline/Hybrid/Blazor gates; approval and issue closure remain external |

## Implemented increment

- `WebViewLocalContent`: copied bounded allowlist, isolated per-collection HTTPS origin,
  canonical ASCII paths, explicit MIME, fixed CSP and other security headers, no CORS,
  GET/HEAD, empty 400/404/405/410, pure concurrent response resolution and read-only streams.
- Additive `NavigateToLocalContent`, `LocalContent` capability/request property and optional
  `IWebViewLocalContentPeer`; existing adapters keep ordinary operations and fail closed
  for unsupported local content. No native types enter shared API.
- WebView2 Document request interception and API23-compatible Android interception with
  worker-safe immutable snapshots, retirement before cleanup, reload/cancel/replay semantics.
- Both samples load HTML/CSS/JS/PNG without a server. Native fixtures cover CSP inline,
  frame/connect denial, missing image, reload, replacement, canceled replacement, inactive
  origin and Windows local reparent/replay alongside the full pre-existing baseline path.
- Deterministic path/encoding/authority/method/MIME/size/immutability/threading negatives,
  unsupported capabilities, stale/reentrant document callbacks and Source/ID trust regression.
- XML docs/examples; local-content and Hybrid security decisions; architecture/public API,
  capabilities, security, testing, README, roadmap and historical-review cross-references.

No framework edits, second host, new dependencies, directory/file access, HTTP listener,
privileged RPC, reflection exposure, ASP.NET Core, version assignment or publication.

## Validation

Final results are recorded after execution below; historical PR #9 evidence is not reused
as a new PASS. Commands follow [testing](testing.md), with the external pinned source root,
SDK 10.0.401, sequential `-m:1 -p:UseSharedCompilation=false` and package generation off.
Android uses the installed JDK21/SDK36 workload and the explicitly selected emulator.

| Check | Current result |
| --- | --- |
| Bootstrap / solution restore | PASS; exact pin, external checkout, no checkout overwritten |
| Debug solution build / tests | PASS; 0 warnings/errors; 110/110 (83 shared, 15 Windows, 12 Android), no skips |
| Release solution build / tests | PASS; 0 warnings/errors; 110/110, no skips |
| Windows Debug / Release native | PASS both; WebView2 155.0.4283.60, full baseline and local resources, cancellation/reload, CSP, missing resources, 50 leases, initialization races and local reparent/replay |
| Android Debug / Release emulator | PASS both; signed APKs built with no warnings/errors, installed only on emulator-5554, API34 x86_64 density420, com.google.android.webview/113.0.5672.136; full baseline and local fixtures RUNTIME PASS |
| Android rotation / Home-resume | PASS Debug local document replay, fresh Activity/peer generation 1 -> 3; original rotation settings restored; screenshot inspected (limited visual evidence only) |
| Hosted CI implementation head | PASS all three jobs (shared/Windows/Android) on `50bdb43033febcad258a1322393f9034247fca3c`; [push run 39037796081](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39037796081). Documentation-only delivery updates rerun checks on PR #35. |
| Foreground keyboard injection | NOT RUN; no foreground interactive qualification attempted |
| API23 runtime / physical device / Visual Studio Designer / full IME/DPI/accessibility/heap soak | NOT RUN; outside available qualification in this increment |
| Template verification | Not applicable; main templates/reference app untouched |

Initial validation exposed NU1008 from inherited central versions, an expected WebView2
navigation failure for empty 410 and Android's generated error DOM. Bootstrap and fixture
semantics were corrected; no empty browser DOM is promised for a denied request. An early
accidental build/test overlap produced file-lock warnings; those runs are not final build
evidence. Sequential checks passed afterwards without relaxing tests, retries or security.

Final review also added a regression for Reload of a retained unsupported local request:
it returns false and cannot bypass capability rejection. Relative native callback URIs
are rejected without reading AbsoluteUri on a relative value. The native suite does not
replace physical/API23/retained-heap/privileged-origin qualification. No AAB or publication
was attempted in this increment.

Commands executed: bootstrap; solution restore; sequential Debug and Release solution
builds and `dotnet test --no-build --no-restore`; Windows sample `--validate` in both
configurations; Android sample `-t:SignAndroidPackage` in both, explicit JDK21;
`adb -s emulator-5554 install`, explicit validation Intent, PID-filtered logs, rotation
and Home/resume. Portable build/native commands are in [testing](testing.md).

## Limits and next sequence

This is bounded **static public content**, not authenticated Hybrid content or a network
sandbox. Android request interception is incomplete for redirects; its string interface
cannot identify caller frames. CSP excludes frames/workers/network fetch and inline code;
native application ExecuteScriptAsync remains explicitly authorized. Browser-normalized
traversal can only select public allowlisted keys. History does not keep old collections
mapped and native cross-origin navigation is rejected; cancel may revoke visible-page
resources until local Reload reapplies its collection.
Response streams already handed to an engine remain readable after retirement; no new
private I/O or callback is involved. See [local-content](local-content.md) and
[hybrid-security](hybrid-security.md) for the complete policy and native API references.

Next bounded increments: #33 broader native hostile-origin/frame/redirect/lifecycle evidence,
then #11 transport and #12 RPC in separate reviewable changes; #16 can run independently as
an origin/frame feature prototype without enabling privileged Android calls. Only then #15,
#17 where supported, and #13/#18/#19/#20. #21 is a separate release gate. #22 is optional
and must deliver a runnable go/no-go experiment before any #23-#31 implementation.
The complete dependency/release DAG is in [ROADMAP](../ROADMAP.md).

## Delivery

Delivered as [PR #35](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/35),
targeting main, intentionally OPEN for review.

- Bootstrap: `50d054d` — isolate the pinned dependency checkout from central NuGet inheritance.
- Implementation: `50bdb43033febcad258a1322393f9034247fca3c` — bounded local resources,
  both adapters, examples, regressions, policy and dependency/release documentation.
- Hosted implementation CI: [39037796081](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39037796081)
  is terminal SUCCESS on that exact implementation SHA, with shared, Windows and Android
  jobs all successful. [PR run 39037798953](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39037798953)
  and any documentation-only follow-up checks are linked by the PR; their status is not
  inferred from this earlier run. The final chat/PR check summary records the final head.

This report's delivery update changes documentation only. Local execution evidence above
qualifies the implementation, while hosted checks remain separately identifiable by SHA.
No issues were closed, no PR merged and no NuGet/preview/stable/GitHub release published.
