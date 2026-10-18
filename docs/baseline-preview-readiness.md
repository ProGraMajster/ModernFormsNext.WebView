# Independent baseline preview/stable readiness (#7/#8)

Assessment date: 2026-10-18. **Preview publication: NO-GO today.**
This branch starts from normally merged main b9a5c5e. It has no dependency on the
new policy/Blazor/world branches. Baseline #7 and stable #8 have their own gates;
optional Hybrid #21, #15 or Blazor #22 are not baseline release prerequisites.
No package/version/metadata, source pin or main-framework file is changed.

## Official framework compatibility is a real gate

The live NuGet index and GitHub releases select stable ModernFormsNext1.11.1
(v1.11.1 published 2026-09-22). ModernFormsNext, WindowKit and Backend.Windows have
official1.11.1 packages; Backend.Android's public NuGet index returns HTTP404.

The isolated official-package probe uses SDK10.0.401 and the official NuGet source.
It first compiles existing public Form/IWindowImpl as a control, then separately
compiles NativeViewHost, INativeViewFactory and WindowsNativeViewSite. It keeps its own
props/targets/output directories and disables inherited central package version selection
only for those temporary projects. It never changes product references or the approved
framework source pin 69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3.

    ./scripts/probe-official-framework.ps1 -PackageVersion 1.11.1

The script preserves all logs, restored package SHA256 hashes and result.json under
ignored artifacts/official-framework-probes. Exit2 means the required public API is
missing and is an explicit **NO-GO**, not a passing release test. Other unexpected
errors are inconclusive failures. Hosting symbol availability alone would still not
qualify native behavior, packaging or publication.

Final official-package control compilation passed Debug/Release with zero warnings/errors.
The separate hosting compilation failed both configurations with exactly three CS0246
errors: NativeViewHost, INativeViewFactory and WindowsNativeViewSite are missing.
No other error or warning was present. This is **NO-GO**, not a passing consumer test.
Evidence is retained under artifacts/official-framework-probes/130c69795e5f4a39a974cf4439ea1b1f.
This is the existing publication gap tracked by #7/#8 and the source
bootstrap guard, not a newly invented framework bug. No duplicate framework issue is
created and the main repository stays read only.

| Official1.11.1 package | Restored nupkg SHA256 |
| --- | --- |
| ModernFormsNext | 22D8C6A39D4178AD03ADD48899F0BD03BBE586F8153252B91F6BEADF6A0ECF3A |
| WindowKit | ECFE66E6C734FAA55A904A56ED83EDB90093BA37E717C2FA3271353A5928D31F |
| Backend.Windows | A48BCF8B9153DCB26289CF373BEE9C12489D10EE8455238191A863ED2471C7AC |

Fresh independent-baseline full restore/sequential Debug/Release builds passed with
zero warnings/errors;207/207 tests each (158 shared,37 Windows,12 Android), zero skipped.
Native Windows and emulator-5554/API34 both reached RUNTIME PASS in both configurations.
Evidence: artifacts/baseline-independent. This branch adds no product runtime code.

## Independent release checklist

| Gate | Current evidence | Before preview #7 |
| --- | --- | --- |
| Architecture/source baseline | Public NativeViewHost and typed sites at approved pin; real Windows/Android adapters | Keep canonical host/lifetime/focus/dispatcher; no duplicate infrastructure |
| Official framework dependency | 1.11.1 lacks required hosting API; Android package index404 | Publish/approve compatible official framework packages through their own process, then test isolated consumers without changing this task's pin |
| Public API | Experimental XML/docs/inventory and neutral contracts exist | Agree preview API/compatibility policy and boundaries; no stable API promise yet |
| Automated checks | Debug/Release regression and exact-head CI recorded per PR | Terminal successful CI for exact release candidate; preserve all warnings/failures/skips |
| Native Windows | WebView2 runtime155.0.4283.62; navigation/scripts/lifetime, 50 create/dispose leases, placement/modal/focus basics | Explicit supported Windows/runtime matrix; foreground keyboard/full IME/DPI/accessibility/manual visual and sustained heap qualification |
| Native Android | API34 x86_64 emulator/provider113, fixed serial; baseline lifecycle/navigation/string/RPC/content checks | API23 floor runtime and physical devices/providers; Activity recreation, IME/DPI/focus/accessibility and memory qualification |
| Security | JS off by default; permission/download/new-window denials; untrusted messages; documented navigation/interception limits | Re-audit candidate defaults/TLS/inputs/resources; declare precise guarantees; no privileged messaging implied |
| Packaging | Product currently uses source references and IsPackable=false; no release artifacts | Review IDs/dependencies/TFMs, native loader/assets, Android metadata, license/README/SourceLink/symbols and package consumer tests |
| Trimming/AOT | Full qualification absent; earlier isolated Blazor trim/COM/linker findings are separate | State baseline support honestly and qualify intended modes; no inference from research builds |
| Documentation/support | Experimental README, platform/security/capability/build docs | Candidate install/startup/migration/troubleshooting/support matrix and known limitations |
| Approval/publication | No release/version selected or publication authorized | Explicit maintainer authorization after concrete candidate review |

For #8 stable, additionally agree the compatibility contract, supported platform/provider
commitment, diagnostics/support policy, long-running reliability/performance evidence
and upgrade/rollback qualification. Stable is not implied by preview or green CI.

The basic native tests do not substitute for the missing manual lanes. API34 does not
prove API23. A create/dispose loop is not a heap soak. Research AndroidX Activity
recreation is not product Activity recreation qualification. No unattended foreground
keyboard/UIA or human visual result is claimed here.

## Next steps

1. Review open implementation/research PRs independently; none is automatically merged.
2. Resolve the official framework/package gate through its own approved release process.
3. Qualify baseline Windows/Android physical/API23/manual lanes and document support.
4. Implement/review packaging and isolated package consumers in a separate authorized
   change, then request explicit publication approval. No package is produced here.
5. Continue optional Hybrid/Blazor only after their actual authorization/IPC gates pass.

Primary release/index evidence:
[framework v1.11.1](https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.11.1),
[ModernFormsNext NuGet](https://www.nuget.org/packages/ModernFormsNext/1.11.1),
[WindowKit NuGet](https://www.nuget.org/packages/ModernFormsNext.WindowKit/1.11.1),
[Windows backend NuGet](https://www.nuget.org/packages/ModernFormsNext.WindowKit.Backend.Windows/1.11.1).
