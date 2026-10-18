# Independent baseline preview/stable readiness (#7/#8)

Assessment date: 2026-10-18. **Development via pinned current-master snapshot;
package alignment deferred until official ModernFormsNext 1.12.0.**
Preview is **NOT READY — waiting for official ModernFormsNext 1.12.0 and release
qualification**. The owner confirmed that NativeViewHost belongs to 1.12.0.
There is no request to retrofit or republish 1.11.1.

The original independent contribution started at main b9a5c5e; its final audit integrates
accepted #47/#48/#49 without losing their code or documentation. Ordinary WebView #7/#8
still has independent gates: optional Hybrid #21/#15 and Blazor #22 are not prerequisites.
No package/version/metadata, main-framework source or historical baseline pin is changed.

## Current source development and future package alignment

The #47–#50 merge audits used master snapshot
1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1 in a separate detached checkout.
Master advanced during audit: next development uses
7cfa44e9a992c30d5b1cf5fa76a147eeb2539f56, captured2026-10-18T16:17:09Z and frozen in
eng/framework-snapshots.json. Historical baseline remains69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3.
NativeViewHost, NativeViewHosting and the typed Windows/Android native-site source files
are identical across all three revisions. The upstream increment adds SearchBar/TimePicker
and related Designer/control work, without changing those hosting seams.
Fresh full Debug/Release WebView tests/native
qualification on the recorded current snapshot support **GO for source development**;
they are not qualification of an official NuGet package or a stable ABI promise.
Each report identifies its framework SHA; no build/CI follows a floating master.
The [delivery report](next-phase-delivery-report.md) distinguishes executed lanes;
[isolated consumers](testing.md) make external source integration repeatable.

After actual 1.12.0 publication, verify real NuGet availability, package identities,
compatible versions, TFMs and both backends including Android. Download official artifacts,
inspect public API/ABI and native assets/metadata, build isolated Windows/Android consumers,
run full and native qualification, then prepare a separate package-alignment PR.
A project version or tag alone does not satisfy this gate. No release is forced here.

The alignment PR must inventory official ModernFormsNext, WindowKit, Backend.Windows and
Backend.Android identities/compatible versions and actual TFMs (shared net10.0, Windows
net10.0-windows10.0.17763.0, Android net10.0-android/API23 floor). Do not infer Android
availability from Windows. Verify NativeViewHost, INativeViewFactory, INativeViewSite,
typed sites/dispatcher contracts, assembly identities and transitive version closure.
Compare source API against official ref/runtime assemblies, compile isolated consumers
and exercise real hosts/native loader/ABI, Android manifest/JNI assets and architectures.

Keep source CI while adding a separate package-only lane without source fallback.
Review WebView package IDs, experimental API policy, native/transitive assets,
README/license/SourceLink/symbols and versioning in that future PR. This task chooses
no version or metadata change. Re-run full Debug/Release/native and release qualification
against the final candidate before requesting publication approval.

## Historical expected negative package control — 1.11.1

The live NuGet index and GitHub releases select stable ModernFormsNext 1.11.1
(v1.11.1 published 2026-09-22). ModernFormsNext, WindowKit and Backend.Windows have
official 1.11.1 packages; Backend.Android's public NuGet index returned HTTP404.
The missing hosting contracts are expected before 1.12.0, not a new defect or a reason
to stop source development, create a duplicate issue or request a 1.11.1 patch release.

The isolated official-package probe uses SDK10.0.401 and the official NuGet source.
It first compiles existing public Form/IWindowImpl as a control, then separately
compiles NativeViewHost, INativeViewFactory and WindowsNativeViewSite. It keeps its own
props/targets/output directories and disables inherited central package version selection
only for those temporary projects. It never changes product references or the approved
framework source pin 69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3.

    ./scripts/probe-official-framework.ps1 -PackageVersion 1.11.1

The script preserves all logs, restored package SHA256 hashes and result.json under
ignored artifacts/official-framework-probes. Exit2 means the required public API is
missing and is an explicit **NO-GO for that package consumer**, not a passing release test.
For 1.11.1 this is the expected historical control. Other unexpected
errors are inconclusive failures. Hosting symbol availability alone would still not
qualify native behavior, packaging or publication.

Final official-package control compilation passed Debug/Release with zero warnings/errors.
The separate hosting compilation failed both configurations with exactly three CS0246
errors: NativeViewHost, INativeViewFactory and WindowsNativeViewSite are missing.
No other error or warning was present. This is the expected negative 1.11.1 consumer result.
Evidence is retained under artifacts/official-framework-probes/130c69795e5f4a39a974cf4439ea1b1f.
The owner has scheduled these contracts for 1.12.0; no 1.11.1 repair is required.
The source bootstrap guard remains deliberate. No duplicate framework issue is
created and the main repository stays read only.

| Official1.11.1 package | Restored nupkg SHA256 |
| --- | --- |
| ModernFormsNext | 22D8C6A39D4178AD03ADD48899F0BD03BBE586F8153252B91F6BEADF6A0ECF3A |
| WindowKit | ECFE66E6C734FAA55A904A56ED83EDB90093BA37E717C2FA3271353A5928D31F |
| Backend.Windows | A48BCF8B9153DCB26289CF373BEE9C12489D10EE8455238191A863ED2471C7AC |

Original independent-baseline validation used the historical source pin before the policy
merge. Full restore/sequential Debug/Release builds passed with
zero warnings/errors;207/207 tests each (158 shared,37 Windows,12 Android), zero skipped.
Native Windows and emulator-5554/API34 both reached RUNTIME PASS in both configurations.
Evidence: artifacts/baseline-independent. The subsequent integrated candidate has 245 tests;
its current-master and historical-baseline lanes are recorded separately in the final PR
audit. This readiness contribution adds no product runtime code.

## Independent release checklist

| Gate | Current evidence | Before preview #7 |
| --- | --- | --- |
| Architecture/source baseline | Public NativeViewHost and typed sites at approved pin; real Windows/Android adapters | Keep canonical host/lifetime/focus/dispatcher; no duplicate infrastructure |
| Official framework dependency | Expected1.11.1 negative control; source development uses a recorded current-master SHA | Wait for actual official 1.12.0, verify available packages/TFMs/backends, then separately align and qualify consumers; no1.11.1 workaround |
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

1. Continue source development and reproducible QA on a recorded current-master SHA;
   retain the historical pin as an explicitly named comparison lane.
2. Qualify baseline Windows/Android physical/API23/manual lanes and document support.
3. After official 1.12.0 is genuinely available, separately align dependencies and qualify
   package/API/ABI/native consumers, then review packaging and request publication approval.
   Optional Hybrid/Blazor retains its own authorization gates.

Primary release/index evidence:
[framework v1.11.1](https://github.com/ProGraMajster/ModernFormsNext/releases/tag/v1.11.1),
[ModernFormsNext NuGet](https://www.nuget.org/packages/ModernFormsNext/1.11.1),
[WindowKit NuGet](https://www.nuget.org/packages/ModernFormsNext.WindowKit/1.11.1),
[Windows backend NuGet](https://www.nuget.org/packages/ModernFormsNext.WindowKit.Backend.Windows/1.11.1).
