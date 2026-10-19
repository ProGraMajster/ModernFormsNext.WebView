# Next-phase delivery and qualification

Assessment: 2026-10-18. Ordinary WebView development remains independent of optional
Hybrid/Blazor authorization. No publication, version selection, milestone/project change
or main-framework source modification is performed.

## Accepted existing PRs

Every complete diff was reviewed for public compatibility, security, native lifecycle,
reentrancy, stale callbacks, resource bounds, tests and documentation. No authenticated
messaging or privileged handlers are enabled.

| PR | Original HEAD | Final audited HEAD | Ordinary merge |
| --- | --- | --- | --- |
| [47](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/47) | 140fafe201dac789c1351a629836983f603d64c1 | 140fafe201dac789c1351a629836983f603d64c1 | ba7260a0dd942bf407a264e1b1a0f0f01c4dfa7a |
| [48](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/48) | 93505cccdcf36e4adcb8f0f78475df24c86ca8c7 | a0e8729142ee3c704c1c1347b94afbd858ba63e1 | a98573c171d7c27c5bbc63c6a144ca34103cac9d |
| [49](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/49) | c87bd6e5d3ffc8ec896eedb590c0c2a2c02c2064 | b998bba5f18a56aaf3ef3a7a71a2ee341aae2c89 | 6c4eb9c72919e7e152e2205ef3ca7f19718d56f9 |
| [50](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/50) | 3b9a088106671c86bb45df288e000f49ed9bbac8 | f9401a1b9760b0bad26a115300ae81438b880b24 | 49b4d1f50d85fcb627299d517e294a7f1f22a2cc |

Each merge used --merge --match-head-commit, has both expected parents and the exact
reviewed head tree. #48/#49 were retargeted/integrated normally after their dependencies,
without conflicts or lost changes. #49 additionally removes one trailing blank EOF line;
no research behavior changed. Independent #50 was integrated after #49 without conflicts;
reports were corrected for accepted merges and the owner's1.12.0 decision. No force push,
squash, rebase or branch deletion. Local main fast-forwarded after every merge, finally
to49b4d1f50d85fcb627299d517e294a7f1f22a2cc.

| Exact reviewed HEAD CI (all jobs SUCCESS) | Post-merge main CI (all jobs SUCCESS) |
| --- | --- |
| #47 [39135940341](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39135940341), [39135936505](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39135936505) | [39141866046](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39141866046) |
| #48 [39141876403](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39141876403), [39141873064](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39141873064) | [39142524331](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39142524331) |
| #49 [39142618103](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39142618103), [39142614253](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39142614253) | [39143137816](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39143137816) |
| #50 [39143529566](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39143529566), [39143526639](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39143526639) | [39144218625](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39144218625) |

Those CI runs use historical69c5ea2. Fresh local #47/#48/#49/#50 audits independently
used master1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1: full restore, sequential
Debug/Release builds,245/245 tests each (196 shared,37 Windows,12 Android), zero
warnings/errors/skips; real Windows and emulator-5554/API34 passed both configurations.
#50 also repeated all those checks on historical69c5ea2. Ignored evidence directories:
artifacts/audit47-master, audit48-master, audit49-master, audit50-master and
audit50-historical-baseline. Original #50's207 tests remain historical pre-policy evidence.

Additional #48 Debug/Release build/contracts and both native policy modes passed
(artifacts/audit48-blazor-master): original Blazor.start resolves, exact manifest/base/CSP
and self JSON support pass; real AttachPage is observed and rejected before forged
traffic, TryDispatch remains false and no managed page/DI handlers exist. Default CSP
denials remain positive controls. Interactive Blazor remains NO-GO.

Additional #49 Debug/Release research build/native/profile cleanup passed
(artifacts/audit49-world-master). Real main/same-origin/opaque actors execute; page-world
binding is unavailable but DOM confused-deputy commands reach the genuine world counter.
Live Stop callback and separately labelled recorded packet are denied. Actual302
127-to-localhost redirect, same-URI reload, JS re-enable, cross-window canonical generation
and fresh controls/disposal pass. This is diagnostic NO-GO evidence.

#44 is CLOSED after explicit API acceptance. #45 stays OPEN/PARTIAL (API23/physical and
other missing lanes). #46 remains a tracking issue. #15 stays OPEN/PARTIAL; #17/#13/#18
stay BLOCKED, with #23–#31 not fully implemented.

## Source development versus official packages

Development via pinned current-master snapshot; package alignment deferred until
official ModernFormsNext1.12.0. Preview is NOT READY — waiting for official
ModernFormsNext1.12.0 and release qualification.

Master advanced during audit. The manifest freezes
7cfa44e9a992c30d5b1cf5fa76a147eeb2539f56, captured2026-10-18T16:17:09Z in a separate
detached read-only checkout. Earlier1fc6d8a results retain their identity.
NativeViewHost.cs, NativeViewHosting.cs and both typed native-site files are identical
across69c5ea2,1fc6d8a and7cfa44e. Upstream adds SearchBar/TimePicker and related
Designer/control work. The historical pin remains a separate manifest/CI lane; the
no-argument bootstrap retains historical behavior.

The development PR adds a validated snapshot resolver, safe external bootstrap, six
manifest-pinned CI jobs (existing shared/windows/android names select CurrentMaster),
an accurate source-root diagnostic and isolated Windows/Android source consumers.
Neither build nor CI follows floating master. Consumers have independent build/central
version boundaries and compile real public hosting/site/WebView/transport/RPC/local-policy
contracts. Windows execution is headless; Android is a real Android-TFM compile.
Logs/result.json retain exact source commits, configuration, SDK and evidence mode.
Initial full main qualification on7cfa44e passed Debug/Release245/245 tests each, real
Windows/API34 in both configurations, zero warnings/errors/skips
(artifacts/audit-main-master-7cfa44e). The new external smoke exposed SDK buildTransitive
targets adding unused WPF/WinForms wrappers and MSB3277 WindowsBase conflicts. The
Windows adapter now keeps those build assets private; genuine Core and all three native
loaders still reach consumers. No warning suppression or inherited repository target masks
the problem. Consumers assert those assets and absence of UI wrappers, with -warnaserror.

Final consumer fixtures passed Debug/Release on both snapshots/platforms: Windows
headless contracts/assets and Android Android-TFM compilation, zero warnings/errors.
Evidence under artifacts/source-consumers: CurrentMaster Windows65154d0abe9749d799c0e28dc1640711,
Android2b9a31acbb5442d5967e77bb99b4b85e; HistoricalBaseline Windowsaf77e4c2a8c344bcab68c4e6f459c56c,
Android648ae02a6cf24b5481b0658febaab03c. Earlier fixture compile errors (Android generated
namespace collision and ambiguous Path) were corrected in the fixture and rerun; they are
not product qualification passes. Wrong existing SHA/nested bootstrap destinations were
explicitly rejected without mutation; all changed PowerShell scripts parse successfully.
The post-fix full current-master qualification passed Debug/Release245/245 tests and
native Windows/API34 in both configurations, zero warnings/errors/skips
(artifacts/development-current-master-final). The first post-fix full build exposed direct
Core SDK use without explicit references in Windows tests/research (13 missing-symbol
errors); both projects now explicitly reference the same centrally selected SDK. Older
d272100 CI39145411411/39145436173 correctly failed Windows; no test was weakened.
Corrected implementation305abf4d0ecdbe7b13bd529ed375171a2ed2389e has terminal SUCCESS
in all six jobs of [push39145536133](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39145536133)
and [PR39145539430](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39145539430).
Post-fix historical69c5ea2 full restore, Debug/Release245/245 tests and native
Windows/API34 both configurations also passed, zero warnings/errors/skips
(artifacts/development-historical-baseline-final). World research builds with explicit
Core SDK references passed Debug/Release on7cfa44e (artifacts/development-world-Debug.log
and development-world-Release.log). Full product/native validation used the corrected
305abf4 implementation; subsequent changes only refine CI/consumer metadata and reports.
GO for source development is qualified separately for both frozen source lanes.

The final CI-only checkout refinement explicitly selects the PR head/push SHA instead
of a synthetic PR merge. Consumer results also record dirty working-tree status.
The [development PR51](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/51)
remains OPEN against main; its final delivered SHA/terminal CI are recorded in the PR
description after the last report commit. No new PR is automatically merged.

Final #51 delivery:4c76107097344fa9f27ffa6b0a3b74068f5ed396. Both exact-head
[push39146423726](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39146423726)
and [PR39146427487](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39146427487)
are terminal SUCCESS in all six jobs. The final clean-head Windows consumer also passed
Debug/Release and recorded dirty=false (artifacts/source-consumers/207792861fc84c00abbd641d4d55f0b9).
This result was verified before starting the dependent #46 increment.

## One implemented #46 increment

The separate [navigation-scheme PR52](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/52)
is based on final #51; both new PRs remain OPEN.
WebView.AllowedNavigationSchemes is per-instance UI-thread state with flags None=0,
Http=1, Https=2, AboutBlank=4, Default=7. Default/static ValidateUri/CSP bytes and old
IWebViewPeer/settings/request constructors remain compatible. App URI checks run before
native submission/token mutation; reported top-level violations precede public Starting,
raise InvalidAddress and cancel document operations. Changed settings use existing Stop/
request IDs/lifetime and survive native recreation. No extra host/focus/history/counter.

Optional IWebViewNavigationFilterPeer detects installed hooks through capability
NavigationSchemeFiltering: initialized Windows Core, Android API24+ active main-frame
client; API23/older peers reject the nondefault option explicitly. Unsupported queued
loads fail Engine without submission, ready changes/loads throw NotSupportedException,
Reload/history return false. Default remains compatible. Raw HTML is separate, so None
is not offline mode. Local catalog origin/CSP remains conjunctive. Re-enabling Http is
an explicit higher-risk choice subject to OS cleartext/TLS rules. Complete option docs,
safe/risky examples, future-control inventory and native matrix:
[navigation-scheme-filter.md](navigation-scheme-filter.md).

The implementation also fixes existing cancellation reentrancy at Submit/Stop/history/
native-begin boundaries: only the captured canonical request may overwrite/stop/settle;
callbacks submitting newer work win. Stop still executes cleanup when cancellation code
throws. No new lifecycle identity or global setting is introduced.

Debug deterministic243/243 shared tests passed, including47 added cases; original
37 Windows/12 Android give292/292 overall. Native Debug Windows and API34 passed real
main/same/opaque actors, typed callback denial, Stop/raw reload, None/raw distinction,
HTTPS catalog and POST fallback. Windows additionally passed owned HTTP302 and actual
cross-window policy/capability/RPC recovery. Initial fixture raw-string compilation
errors and an immediate Task.IsCanceled timing assumption were corrected; final async
assertion awaits bounded cancellation settlement, without skips or policy relaxation.
The committed implementation is ba85b6387685e82d731884d5987e794e9cbb8a9e. Full
current-master restore/sequential Debug/Release292/292 and native Windows/API34 both
configurations passed, zero warnings/errors/skips (artifacts/navigation-final-current).
Exact-head [push39148012175](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39148012175)
and [PR39148093526](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39148093526)
are terminal SUCCESS in all six jobs each. Historical69c5ea2 full restore/Debug/Release292/292
and native Windows/API34 both configurations also passed, zero warnings/errors/skips
(artifacts/navigation-final-historical). Subsequent report/platform-guide edits change no
compiled source; final delivered SHA/terminal CI are recorded in the PR description.

All four updated external source consumers passed restore/Debug/Release, with warnings
treated as errors; Windows headless execution confirms Core/all native loaders/no UI
wrappers. They compile the new public enum/optional interface as well as old contracts.
Evidence under artifacts/source-consumers: CurrentMaster Windows1420ba7662454bedbe6168622d18418e,
Androiddc84097e4e584f6480506eb5a723f728; HistoricalBaseline Windows6aa542f82c894859867f2d63f5dfcdf3,
Androidc97e94a9375245f3987ee837c51ee328. Results identify ba85b638 and dirty=true because
only platform/report Markdown edits were in progress; compiled product sources matched
that implementation commit. Exact-head hosted CI uses clean explicit head checkouts.

Fresh current7cfa Blazor Debug/Release builds/contracts, native Default/Application modes
and profile cleanup passed (artifacts/navigation-blazor-current). Real AttachPage remains
rejected, TryDispatch false, no managed page or privileged DI scope. Fresh current7cfa
world Debug/Release build/native/profile cleanup passed (artifacts/navigation-world-current):
positive real actor controls, DOM confused-deputy counterexample, live Stop and recorded
packet rejection, actual302, same-URI reload and real host/context recreation. These
research execution passes preserve interactive/privileged NO-GO. This is regression of
existing fixtures, not implementation of the new planned trust experiment.

Android POST may already have network effects. Android cross-scheme HTTP302 is NOT RUN
(cleartext manifest retained); reported redirect unit coverage does not qualify all native
redirects/providers. API23 runtime/control branch, physical devices and Android product
Activity recreation remain NOT RUN. Scheme choice never authenticates a caller;
IsOriginAuthenticated=false and absent AuthenticatedMessaging/privileged handlers remain.

## One next trust experiment: plan only

No additional research implementation is claimed. Blazor original assets/start can load
under explicit Application CSP, but real AttachPage remains unauthorized/rejected and
interactive/privileged DI/IPC remains NO-GO. Isolated-world binding separation alone
failed the whole-realm argument because shared DOM can act as a confused deputy.
Android AddJavascriptInterface never authenticates the original caller.

The next bounded experiment is a Windows closed-program realm with an app-owned,
one-use permit for a diagnostic IncrementOwnedCounter(delta=1) operation. It may not
touch files/network/DI or add production handlers. The only positive invocation comes
from a native app action with a permit bound to current canonical peer/document,
operation/arguments and the engine-reported execution context. Merely possessing a
binding, URL, public request ID or context label is insufficient.

Before running it, audit the entire trusted program/call graph: no DOM read, DOM event,
page-controlled callback/prototype/object, message or page-derived argument may influence
the authorized call. Use engine-created/context-attributed execution and clear permits
on document/context destruction, Stop, JS disable, recreation and disposal. Fail closed
if context/lifetime attribution or program closure cannot be demonstrated.

Attacker controls page main/same-origin/opaque frames, shared DOM and all page messages.
Positive control: one app-authorized exact operation increments once. Negative controls:
direct page binding attempts, DOM deputy commands, forged/wrong-context packets, wrong
operation/arguments, replay/duplicate permit, stale document/context, redirect/same-URI
reload, Stop/JS-disable/recreate/dispose. Each actor must positively execute, each rejected
call leave the counter unchanged, then a fresh authorized positive call must still work.
Keep provenance logs and distinguish live callbacks from deliberately replayed records.

Decision today: NOT RUN/NO-GO until closure and operation authorization are proven.
Even a passing diagnostic counter would qualify only that host-initiated experiment,
not original Blazor AttachPage or general page-to-C# IPC. Actual research needs a
separate project/PR. #15 stays OPEN/PARTIAL, #17/#13/#18 BLOCKED, #23–#31 incomplete.

The expected negative official1.11.1 control proves only that old packages lack these
new hosting contracts; it is not a bug, patch-release request or development blocker.
The explicit-version official probe is retained unchanged. After actual1.12.0 publication,
verify real package identities/compatible closure/TFMs/backends including Android, public
API/ABI/ref/runtime assemblies, native loader/JNI/manifest assets and isolated package-only
consumers. Keep source CI and add package CI without source fallback, then prepare a
distinct dependency-alignment PR and full release qualification. A project version or
tag alone is insufficient. See baseline-preview-readiness.md for the concrete gates.

## Available evidence and missing qualification

Environment: Windows x64 host; SDK10.0.401; WebView2 SDK1.0.4258.31/runtime155.0.4283.62;
JDK21.0.8; Android SDK/build tools36; emulator-5554 only, API34/x86_64,
com.google.android.webview113.0.5672.136. No physical device or unrelated process/profile.
Builds are sequential with -m:1, UseSharedCompilation=false, GeneratePackageOnBuild=false.
Native Windows uses test-owned temporary profiles with verified bounded cleanup;
Android uses a signed sample APK and explicit serial.

API23 runtime, physical devices/providers, foreground keyboard/UIA/human visual, full
IME/DPI/accessibility, Designer integration, retained Activity/native/managed heap soak,
full trim/AOT release qualification: NOT RUN. Fifty leases do not prove leak freedom;
API34 does not prove API23. Hosted CI/headless checks are not native QA.
Official1.12.0 package compatibility: NOT RUN until actual artifacts exist.
Windows x86/arm64 native execution and other Android ABIs: NOT RUN; copied loader files
are an asset check, not execution on those architectures.

Repeatable product commands (then repeat using HistoricalBaseline with its matching root):

    $framework = & ./scripts/bootstrap-framework.ps1 -Snapshot CurrentMaster
    dotnet restore ./ModernFormsNext.WebView.slnx -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true
    dotnet build ./ModernFormsNext.WebView.slnx -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true
    dotnet test ./ModernFormsNext.WebView.slnx -c Debug --no-build --no-restore -m:1 -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true
    dotnet run --project ./samples/ModernFormsNext.WebView.Sample.Windows -c Debug --no-build -- --validate
    dotnet build ./samples/ModernFormsNext.WebView.Sample.Android -c Debug --no-restore -t:SignAndroidPackage -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework -p:JavaSdkDirectory=$env:JAVA_HOME
    adb -s emulator-5554 install -r ./samples/ModernFormsNext.WebView.Sample.Android/bin/Debug/net10.0-android/com.programajster.modernformsnext.webview.sample-Signed.apk
    adb -s emulator-5554 shell am force-stop com.programajster.modernformsnext.webview.sample
    adb -s emulator-5554 shell am start -n com.programajster.modernformsnext.webview.sample/.MainActivity --ez validate true
    $samplePid = adb -s emulator-5554 shell pidof -s com.programajster.modernformsnext.webview.sample
    adb -s emulator-5554 logcat -d --pid=$samplePid -s MFNWebView:I
    ./scripts/test-source-consumer.ps1 -Snapshot CurrentMaster -Platform Windows -FrameworkRoot $framework
    ./scripts/test-source-consumer.ps1 -Snapshot CurrentMaster -Platform Android -FrameworkRoot $framework -JavaSdkDirectory $env:JAVA_HOME

Repeat product build/test/native commands with Release and Release APK path. Require
terminal RUNTIME PASS/exit status in process-owned logs, not old unfiltered logcat.
Separate research: build research/BlazorFeasibility.Windows and research/WindowsWorldProbe
with the same source properties, then each Validate.ps1 -Configuration Debug/Release.
Blazor also runs --validate-contracts. Research is outside the product solution and adds
no product ASP.NET/AndroidX dependency.

Research command details (repeat with Release):

    dotnet build ./research/BlazorFeasibility.Windows -c Debug -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework
    dotnet run --project ./research/BlazorFeasibility.Windows -c Debug --no-build -- --validate-contracts
    ./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug
    ./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug -ApplicationPolicy
    dotnet build ./research/WindowsWorldProbe -c Debug -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework
    ./research/WindowsWorldProbe/Validate.ps1 -Configuration Debug

Native wrappers wait for their owned process trees and verify test-profile cleanup.
The final PR description records the delivered docs-only HEAD/CI separately from the
compiled implementation SHA. No template was changed; template verification was not needed.

## Next actions, in dependency order

1. Review #51, then dependent #52 (the single #46 feature) and their exact-head CI/native
   evidence. Both stay OPEN for the owner's decision; requalify after any later edits.
2. Complete missing baseline API23/physical/provider and Windows foreground/manual,
   IME/DPI/accessibility/Designer/heap/release-mode qualification before claiming support.
3. After actual official1.12.0 publication, verify and align package-only/API-ABI/native
   consumers in a separate PR, then request publication approval. Hybrid authorization
   remains an independent NO-GO gate and does not block ordinary WebView development.
