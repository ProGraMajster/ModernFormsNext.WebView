# Build and qualification

For #46 popup configuration, run `scripts/test-security-consumer.ps1` for both frozen
snapshots (four external compiler cases in Debug/Release). Run Windows sample
`--validate-popup` and Android driver `-Mode Popup`; Android injects Enter only after
checking the sample owns the selected device's foreground window. Windows optional
`--popup-gesture` uses the existing guarded native keyboard injector. Run the ordinary
full/native/default-security regression lanes separately. See [popup semantics and
limits](new-window-policy.md), [options inventory](security-options-roadmap.md) and
[broader platform qualification](native-platform-qualification.md).

The [native platform qualification](native-platform-qualification.md) adds explicit-serial
Android Floor/lifecycle and owned TLS/default-security drivers, real Windows DPI/input
and VS Designer evidence, and retained failures. Consult its limits before acceptance.

Use .NET SDK 10.0.401. Windows requires the installed WebView2 Runtime for native validation;
the adapter references Microsoft.Web.WebView2 1.0.4258.31. Android uses the .NET Android workload
and SDK platform/build tools 36 with JDK 21. The sample minimum is API 23; no AndroidX WebKit is required.

```powershell
$framework = & ./scripts/bootstrap-framework.ps1 -Snapshot CurrentMaster
dotnet restore ./ModernFormsNext.WebView.slnx -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true
dotnet build ./ModernFormsNext.WebView.slnx -c Debug --no-restore -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true -p:GeneratePackageOnBuild=false -m:1 -p:UseSharedCompilation=false
dotnet test ./ModernFormsNext.WebView.slnx -c Debug --no-restore -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true -p:GeneratePackageOnBuild=false -m:1 -p:UseSharedCompilation=false
# Repeat build/test with -c Release. Build sequentially: the dependency has shared generators.
```

The manifest eng/framework-snapshots.json records two immutable revisions. CurrentMaster
selects 7cfa44e9a992c30d5b1cf5fa76a147eeb2539f56, captured from master at
2026-10-18T16:17:09Z, in sibling framework-master-7cfa44e. Updating this entry requires
contract-diff review and full qualification, never a floating branch in a build.
HistoricalBaseline retains 69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3.
For compatibility the no-argument bootstrap still selects HistoricalBaseline.
Its default destination is a sibling `.modernformsnext-webview/framework-69c5ea2` directory,
outside this repository. Do not put a source checkout under this repository: NuGet would
inherit its Directory.Packages.props and reject the framework's explicit package versions.
Existing checkouts are never moved or overwritten. CI already uses sibling directories.
An explicit source root is required: an older published framework package cannot supply this
contract. A missing root/NativeViewHost fails with a useful build error. The project files contain
no machine-specific ProjectReference. Keep dependency clones and build artifacts ignored.
No library is packable in this baseline, and no publication credentials are used.

## Isolated source consumers, separate from official packages

    ./scripts/test-source-consumer.ps1 -Snapshot CurrentMaster -Platform Windows -FrameworkRoot $framework
    ./scripts/test-source-consumer.ps1 -Snapshot CurrentMaster -Platform Android -FrameworkRoot $framework -JavaSdkDirectory $env:JAVA_HOME

Each command restores and sequentially builds Debug/Release in a new ignored GUID directory.
Independent props/targets/central-version boundaries expose actual external integration errors.
They reference public WebView, NativeViewHost, factory, typed native-site, transport/RPC
and local-policy types. Windows also executes headless contracts without engine/platform
initialization. Android compiles the real Android TFM/backend; it does not run an Activity.
Windows also requires Core and x86/x64/arm64 loaders in the consumer output and rejects
unused WPF/WinForms wrappers. The Windows adapter keeps the SDK's build/buildTransitive
targets private: its resolved Core assembly/loader content still flow through project
references. This fixes the external MSB3277 WindowsBase conflict without suppressing
warnings or injecting repository targets into the consumer. Consumer builds use -warnaserror.
Code directly using Microsoft.Web.WebView2.Core types must declare its own SDK reference;
the Windows error-mapping tests and isolated world research do so explicitly. Ordinary
WebView's public platform-neutral API does not require those native SDK types.
These checks prove neither official NuGet/API-ABI compatibility nor native behavior.
Use HistoricalBaseline with its matching root for comparison. Wrong SHA/dirty dependencies
fail before building. Logs/result.json retain both repository identities, SDK and evidence mode.

Official alignment waits for actual ModernFormsNext1.12.0 publication. The unchanged
probe-official-framework.ps1 is an explicit-version diagnostic; the expected1.11.1 negative
control does not block source development. See baseline-preview-readiness.md for gates.

## Deterministic suites

Shared tests use the framework's existing ModernFormsNext.Testing simulated native sites,
implementing only legal public feature-provider/peer interfaces. They cover Source/HTML, latest
request replay, readiness, settings, event order and observer exceptions, cancellation, reentrancy,
request/lease guards, lifetime cancellation without native completion, history availability,
Designer properties, detached placeholder and repeated disposal. Regression coverage includes
disallowed redirects from raw HTML, cancellation callback failures during cleanup and public
signature leakage. They do not run a browser.

Windows tests execute the same internal command-queue source to cover original request identity,
coalescing, Stop-before-acknowledgement, stale acknowledgement after reentrant navigation,
retirement and transport failure. Provider construction
must not initialize the platform or engine. Native COM/controller behavior is covered by the
separate real runtime sample; a queue unit test is not controller validation.

Android host-runnable tests cover stable error mapping and JSON escaping for app-to-page strings.
SDK callbacks, frame filtering, settings, JNI lifetime and Activity integration require the
emulator/device path. No Android behavior is inferred from the host-runnable net10.0 build.

## Windows native path

Ordinary --validate also runs NavigationSchemeRuntimeValidation on Windows/API24+ Android:
real main/same/opaque actors, pre-public-event scheme denial, Stop/raw reload, None/raw
separation, HTTPS catalogs and actual POST. Windows adds owned HTTP302 and cross-window
policy/capability retention. API23 has an explicit unsupported-control path, not a claim
of feature support. See navigation-scheme-filter.md for boundaries and NOT RUN lanes.

Both native validators now also load local HTML/CSS/JS/PNG and verify fixed-CSP inline,
frame and fetch denial, collection replacement/reload and inactive namespace denial.
The Windows cross-window replay fixture also replays local resources. Deterministic
resource tests cover traversal/escaping, authority spoofing, size/MIME/method bounds,
buffer isolation, concurrent readers, stale callbacks and unsupported peer behavior.
These checks do not qualify privileged message origin/frame authentication.

```powershell
dotnet run --project ./samples/ModernFormsNext.WebView.Sample.Windows -c Debug --no-build -- --validate
# Foreground interactive desktop only: adds actual native Tab/Shift+Tab injection.
dotnet run --project ./samples/ModernFormsNext.WebView.Sample.Windows -c Debug --no-build -- --validate-keyboard
```

Build first with the source-root properties above. Validation runs in a real MFN Form and uses
raw offline HTML plus a random-port loopback content server. It checks initialization, Starting/
Completed, cancellation, history, reload/Stop, JSON scripts, both message directions with explicit
acknowledgement, resize, hide/show, native page input focus, ancestor scroll/rectangular placement
clipping, MFN modal owner-input gating, 50 fresh browser leases, cross-window replay/history reset,
isolated initialization failure, and bounded test-profile cleanup after the message loop closes.
It also exercises JSON result shapes, malformed bridge packets, Unicode/quote/backslash message
escaping, native POST identity and reentrant navigation. Windows adds deferred controller focus
and ten immediate retirements while asynchronous controller initialization is outstanding.
Windows also captures an actual old private message, reloads the same origin, and verifies
that replacing only its public request ID cannot deliver it. A positively received current
message fences the negative assertions; guessed/legacy packets and post-revocation recovery
are covered. [#15](issue15-windows-message-hardening.md) documents the authentication limit.
Both samples run the [#19 RPC failure/lifetime matrix](issue19-errors-cancellation.md),
including serialization deadline/no page invocation, conversion/rejection, parallel cancel,
Stop/JS-disable/reload and borrowed wrapper disposal. Windows cancels pending RPC across
real cross-window native-host recreation and proves recovery with the same client.
Failure returns exit code 1. The failure fixture temporarily sets the SDK-supported process-local
WEBVIEW2_BROWSER_EXECUTABLE_FOLDER to a nonexistent fixed-version directory, then restores its
previous value in finally. It does not alter the installed runtime or use an invalid profile
(the latter can display an Edge-owned blocking modal dialog).

The explicit keyboard scenario checks actual Tab/Shift+Tab between HTML elements and the native
HTML-to-MFN boundary. It verifies the foreground process before every SendInput, and fails if
the desktop cannot foreground the sample. A background-shell run must not inject keys into an
unrelated application. Record keyboard qualification separately from ordinary native validation.

This is automated native execution, not a human visual check. Inspect actual browser pixels,
broader keyboard/IME behavior, overlays/airspace and multi-DPI behavior manually before release.
Check that no test-profile browser processes remain after close. Exact process memory equality
is not a leak criterion. A lifecycle exercise is not a managed/Java/native heap-retention proof.

## Android native path

```powershell
dotnet build ./samples/ModernFormsNext.WebView.Sample.Android -c Debug -t:SignAndroidPackage -p:ModernFormsNextSourceRoot=$framework -p:GeneratePackageOnBuild=false -m:1 -p:UseSharedCompilation=false
adb install -r ./samples/ModernFormsNext.WebView.Sample.Android/bin/Debug/net10.0-android/com.programajster.modernformsnext.webview.sample-Signed.apk
adb shell am start -n com.programajster.modernformsnext.webview.sample/.MainActivity --ez validate true
adb logcat -s MFNWebView
```

Use the manifest-generated Activity component if its Java name differs (the sample explicitly
names it). Repeat Release; APK is for local device validation, never store publication.
The validation intent runs the common offline checks, HTTPS navigation/history and native POST
identity/reentrancy checks. Logs record
engine/provider diagnostics and lifecycle generation, without page contents or message payloads.
Rotate the emulator to recreate Activity and verify a fresh peer plus replay. Exercise Home/resume,
close/force-stop, page text input, scrolling, clipping and MFN modal ordering. Record API, ABI,
density and provider/version, and restore any changed emulator rotation settings afterwards.

Do not call a physical-device, API-23, IME, full Tab-boundary, retained-Activity or visual-overlay
check passed based on a build or a callback log. Mark unavailable checks explicitly. The report
records which checks were actually run and which still require qualification.

## CI

The isolated [AndroidX prototype](../research/AndroidOriginProbe/README.md) is built explicitly
outside the product solution. Its validator requires an adb path and explicit device serial;
run Debug/Release with `-Recreate`. Its terminal marker means the experiment executed;
the privileged result is NO-GO. Hosted compilation is not native execution evidence.

The separate [Windows Blazor feasibility probe](../research/BlazorFeasibility.Windows/README.md)
has contract-only mode (`--validate-contracts`) for hosted CI, plus an explicit native
`Validate.ps1 -Configuration Debug/Release` requiring a real WebView2 desktop. It uses
the same canonical WebView/dispatcher and waits for its owned process tree before cleaning
the artifact profile. Terminal execution PASS records **interactive NO-GO**, with positive
connect-src/base-uri denial and rejected untrusted IPC; it is not an interactive Blazor PASS.

The workflow has shared, Windows and Android jobs for both frozen manifest entries.
CurrentMaster keeps existing shared/windows/android check names; historical-baseline
jobs are separate comparisons with fail-fast disabled. Each resolves a validated full SHA
from the checked-out manifest and checks out exactly that dependency. No job follows master.
Windows/Android also compile the isolated source consumers. Shared runs the deterministic
suite on Ubuntu; Windows builds the sample and tests its adapter queue; Android installs its
workload/SDK and explicitly selects JDK 21, runs the pure policy tests and signs a local Release APK. All builds are sequential,
use no publishing secrets and perform no package deployment. Hosted CI execution requires an
authorized push/PR; local command reproduction alone is not a remote CI pass.

## Current Hybrid security qualification

New [typed local policy](local-content-policy.md) validation includes positive raw-HTML
normal/opaque srcdoc script controls, then inherited script denial, real page-script eval,
permissive meta-CSP tampering, HTTP response CSP, self JSON/base support, foreign host/port/
base/script denial, unlisted/method/escaped-path errors, all five selectors, rejected local
departure, replacement/Stop/reload/default recovery and Windows cross-window replay.
Native ExecuteScript is an application operation which can bypass page CSP, so page eval
attacks run from a loaded catalog script. No timeout/skip/policy weakening establishes PASS.
The current Windows/Android engines differ in the safe retained/fallback base path after
a rejected foreign base; the invariant is the declared self-origin boundary, not one path.

The [strategy acceptance matrix](hybrid-trust-strategy.md) distinguishes executed baseline
rejection/correlation tests from unimplemented trusted-realm/world/handler qualification.
Fresh product Debug/Release/native, AndroidX recreation and Windows Razor probe runs for
this strategy are recorded in the [maturity report](phase2-maturity-report.md). No new
runtime code or mirror tests are added merely to make a documentation-only ADR green.
