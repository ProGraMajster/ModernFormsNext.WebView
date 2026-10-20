# Roadmap

The isolated [closed-program counter experiment](docs/closed-program-counter-experiment.md)
measures a native-only one-use diagnostic permit. No product handlers/authentication
are enabled; general Hybrid/Blazor AttachPage remain NO-GO.

The [native qualification continuation](docs/native-platform-qualification.md) records
API23 Floor versus modern RPC, actual Activity/device testing, default security, real
100%/225% monitors and remaining Windows input/restore/Designer failures. These do not
unlock preview, privileged Hybrid handlers or automatic merging of new PRs.

Main framework NativeViewHost: **DONE externally**, through ModernFormsNext #60 / PR #197.
WebView implementation: **IN DEVELOPMENT / EXPERIMENTAL**. The baseline has completed
implementation review; [the review report](docs/final-review.md) lists fixes and qualification
limits. Maintainer acceptance is separate. The baseline does not close issues or select a package release/version.

| Issue | Local status | Review/release gate |
|---|---|---|
| #1 Planning and architecture | Written architecture, ownership, security and compatibility decisions | Review these decisions with the implementation |
| #2 Native content hosting contract | Satisfied externally by main #60 | Link to the accepted main contract; do not duplicate it here |
| #3 Shared WebView API | Implemented, deterministic tests and API inventory reviewed | Maintainer acceptance before public stability/release commitment |
| #4 Windows WebView2 backend | Implemented, native runtime validation | Review policy, callback identity and supported Windows/runtime floor |
| #5 Android WebView backend | Implemented, API34 emulator validation | Review SDK hook limits and interrupted-navigation history reset; physical/API23 qualification remains |
| #6 Samples and tests | Both samples, three test suites and local native scenarios | Extended manual keyboard/visual/leak profiling remains a quality gate |
| #7 Preview NuGet release | NOT STARTED | Packaging against an official framework package, release approval, qualification |
| #8 Stable release | NOT STARTED | API stability, platform commitments and release approval |

- [x] Native hosting prerequisite supplied by the main framework.
- [x] Implementation and public API inventory reviewed for the baseline commit.
- [x] Baseline merged through PR #9 (2026-10-10); release approval is separate.
- [ ] Preview NuGet release.
- [ ] Stable release.

Advanced Phase 2 follow-ups: neutral permissions UX; cookies and request headers
with explicit cross-platform semantics; controlled downloads and new-window policy;
DevTools/zoom policy; Android origin-authenticated messaging (requires an explicit
dependency/API decision); full browser-history/session restoration; wider device/manual
qualification. Main issue #20 remains an umbrella and must not be closed by this baseline.

## Phase 2 dependency and release gates (#34)

The first increment implements [local application resources](docs/local-content.md) for
#32/#14, with the [trust matrix/architecture decision](docs/hybrid-security.md) for #10/#33.
All APIs remain experimental. [phase2-progress](docs/phase2-progress.md) records the live
issue audit, exact implementation/validation status and next steps.

```text
Baseline #1-#6 -> #7 preview -> #8 stable
  (its own API/platform/packaging/approval gates; no Hybrid/Blazor dependency)

#10 architecture -> #32 resources + #33 security matrix -> #14 local content
#10 -> #11 transport -> #12 versioned RPC
#33 + #11 + #12: MERGED; unprivileged correlation/RPC only
#16 Android research: prototype merged, privileged parent-relay policy NO-GO
#14 + #12 + #33 -> #15 Windows: partial hardening; original-caller gate OPEN
#10 trusted whole-application realm strategy: PROPOSED, not authenticated caller identity
#10 reviewed content/realm + native qualification -> fresh #15/#17 security decision (BLOCKED)
#16 new verified trust contract + #12/#14/#33 -> #17 Android transport (BLOCKED)
#12 + verified platform transport -> #13 explicit handlers
#13 + #14 + supported secure transport -> #18 Hybrid control
#12 -> #19 errors/cancellation (unprivileged extension can proceed independently)
#19 privileged errors -> separately verified platform transport
#18 + #19 -> #20 offline sample/diagnostics -> #21 qualification/preview

#22 executable Windows spike: interactive NO-GO under current CSP/IPC contract
#22 new verified content/security contract + fresh GO -> #23 host + #24 assets (BLOCKED)
#23 + #24 -> #25 root components -> #26 interop + #27 lifetime/hot reload
#22 Android GO -> #28/#30 Android feasibility/adapter
qualified implemented platforms -> #29/#31 samples/tests/optional package
```

Stages require executable code and negative tests, neutral public API/XML docs, no second
NativeViewHost, compatible baseline behavior and explicit unsupported capabilities.
Local resources additionally require native MIME/CSP/loading/lifetime validation; static
content is never proof of message authentication. #11/#12 require bounded protocol parsing,
timeouts/cancellation, out-of-order/duplicate/error/version cases and document correlation.
#15/#17 require native hostile-frame/origin/redirect/stale-document evidence. If Android
cannot provide authenticated frame/origin metadata it remains unavailable for privileged RPC.

#21 requires separate supported-platform/physical-device/IME/focus/DPI/lifetime/security
qualification and explicit preview approval. #22 requires a small runnable .NET10 experiment
and a go/no-go ADR using public APIs, with license/DI/dispatcher/AOT/static-asset evidence;
an assumption about another UI framework is insufficient. No Blazor implementation starts
before that GO. #28/#30 and #29/#31 overlap in current GitHub descriptions; consolidation
is a maintainer decision, not automatic issue closure here.

Ordinary WebView may reach stable once its own gates pass. Optional local-resource,
Hybrid, RPC, Android authenticated transport and Blazor gates do not delay that decision.
No version, NuGet publication, release, merge or issue closure is authorized by this DAG.

Follow-up #33 provides native hostile-frame/redirect/lifetime evidence in PR #36.
The [#11 transport](docs/issue11-transport.md) reuses canonical document cancellation
without exposing privileged handlers. #12 is merged as a separate dependent protocol change;
#13/#15/#17 still require independently verified privileged platform messaging.
[#12 RPC](docs/issue12-rpc.md) builds on #11 with explicit unprivileged app-to-JavaScript
calls only; bounded protocol validation is independent of the pending authentication gate.

## Subsequent verified state

Current accepted state: #47/#48/#49 are normally merged after exact-head reviews and
fresh current-master snapshot qualification; #44 API is accepted, #45 is incomplete.
Development continues via a recorded master SHA, with historical 69c5ea2 comparison.
Official dependency alignment waits for actual ModernFormsNext 1.12.0 publication.
The expected 1.11.1 negative control is not a development blocker. Ordinary #7/#8 is
independent of privileged Hybrid/Blazor NO-GO.

## Historical continuation records

Current security/configuration continuation: #41 → #42 → #43 are now normally merged
into main `b9a5c5e`, with [final exact-head audit](docs/pr41-43-final-audit.md) and green
post-merge CI. #15 stays OPEN/PARTIAL; original-caller and interactive Blazor NO-GO remain.
Older open-PR wording below describes historical delivery.

[#44 typed policy](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/44)
and [#45 native effects](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/45)
extend the canonical immutable catalog with None/Self selectors and an explicit Application
profile. [API/effects/risks/examples](docs/local-content-policy.md) retain the exact default
CSP and reject nondefault policy on unsupported peers. Self connections/base permit a new
controlled #22 asset experiment; they do not authorize IPC or full #23–#31.
[#46](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/46) remains OPEN/PARTIAL.
Its first [explicit popup increment](docs/new-window-policy.md) preserves default Deny,
adds a fresh application-owned target and MFNWVSEC001 consumer/runtime warnings.
The [complete options inventory](docs/security-options-roadmap.md) separates implemented
contracts from proposed download/permission/TLS/profile controls. There is no global disable switch.
The next ordered increments are controlled Blazor A-assets/B-IPC research, a bounded
Hybrid trust experiment and an independent baseline #7 readiness checklist. No new PR
from these increments may merge automatically, and no package/release is authorized.

Latest: #39/#40 normally merged, local main `b329da3`. The
[final audit](docs/pr39-40-final-audit.md) records exact heads/CI and fresh full/native
checks. #15 remains PARTIAL/open; the parent-relay model remains privileged NO-GO.
[#19](docs/issue19-errors-cancellation.md) fixes total conversion deadlines and coincident
cancellation/error priority with canonical/native lifetime regressions. Privileged error
mapping stays blocked; #13/#17/#18 remain gated. #22 feasibility and a separate trust
strategy follow in open PRs. Older delivery records below are historical.

#19 is delivered in open [PR #41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41),
final head `1facdc2`, all three exact-head CI jobs SUCCESS; full Debug/Release 207/207 and
both native platforms PASS. [#22 research](docs/issue22-blazor-feasibility.md) now supplies
an executable .NET10 Windows Razor/public-WebViewManager prototype. Passive rendering,
DI, assets, dispatcher and disposal pass, while unmodified interactive startup is blocked
by connect-src/base-uri policy and unverified privileged IPC remains disabled. Full #23–#31
stay gated. Open [PR #42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42)
has final head `17e3c8a` with all three exact-head CI jobs SUCCESS. Its local publish
diagnostics leave trimming unqualified and NativeAOT runtime unexecuted.

The [Hybrid strategy ADR](docs/hybrid-trust-strategy.md) separates whole-app-realm trust
from individual-script identity, defines immutable content/separate unprivileged views,
platform/world research and revocation/authorization acceptance gates. It enables no
capability: neither candidate is qualified; #15 stays OPEN/PARTIAL and #17/#13/#18 stay
blocked. It is delivered in open [PR #43](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/43).
The [maturity report](docs/phase2-maturity-report.md) is the current four-stage
delivery/evidence view. A strategy document does not satisfy privileged transport gates.

PR #36/#37/#38 are merged normally; issues #33/#11/#12 are complete within their scoped
criteria. [Merge audit](docs/pr36-38-final-audit.md) records history, CI and fresh qualification.
[#16 native AndroidX research](docs/issue16-android-origin-research.md) demonstrates exact
origin/direct-frame metadata and a same-origin parent-object relay counterexample. Its
privileged decision is NO-GO for that main-world policy. #17 requires a new verified security
contract; #13 cannot start from an origin allowlist and isMainFrame alone. The research
dependency stays outside product projects. API23 runtime remains an independent gate.

[#15 Windows hardening](docs/issue15-windows-message-hardening.md) adds private packet
tokens, exact local native Source checks and revocation tests to the existing adapter.
It rejects old-packet/current-ID substitution but deliberately leaves authentication
unavailable: same-origin parent-helper relay still defeats an original-caller policy.
Issue #15 remains PARTIAL/open; #17/#13/#18 are not approved by these improvements.

Next independent work: #19 audit/extend typed errors and cancellation already provided
by #12; #22 runnable feasibility/ADR, with no full Blazor host before GO. #20 still needs
#18/#19; its eventual bidirectional sample must wait for verified handler security.
[The current follow-up report](docs/phase2-followup-report.md) records the live issue audit,
stage results, dependency decisions and unexecuted qualification. Baseline #7/#8 follow
their own package/API/platform gates and are not blocked by optional Hybrid/Blazor work.
