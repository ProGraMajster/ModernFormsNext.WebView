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
- [ ] Maintainer approval and merge.
- [ ] Preview NuGet release.
- [ ] Stable release.

Advanced Phase 2 follow-ups: neutral permissions UX; cookies and request headers
with explicit cross-platform semantics; controlled downloads and new-window policy;
DevTools/zoom policy; Android origin-authenticated messaging (requires an explicit
dependency/API decision); full browser-history/session restoration; wider device/manual
qualification. Main issue #20 remains an umbrella and must not be closed by this baseline.
