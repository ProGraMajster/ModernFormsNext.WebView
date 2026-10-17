# Roadmap

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
#16 Android origin/frame research ----------------------+
#14 + #12 + #33 -> #15 Windows secure transport         |
#14 + #12 + #33 + #16 verified -> #17 Android transport <-+
#12 + verified platform transport -> #13 explicit handlers
#13 + #14 + supported secure transport -> #18 Hybrid control
#12 + secure transport -> #19 errors/cancellation
#18 + #19 -> #20 offline sample/diagnostics -> #21 qualification/preview

#22 Blazor feasibility GO + lifecycle/assets contract -> #23 host + #24 assets
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
