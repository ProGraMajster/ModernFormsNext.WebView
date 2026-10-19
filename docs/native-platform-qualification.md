# Native platform qualification

This work extends executable qualification, not preview readiness. The PR report records
the final tested WebView HEAD, frozen framework SHA, configurations, native sample hashes,
providers, CI, results and limitations. Local `artifacts/` logs include serial/PID/run
identities and are deliberately not committed. A later passing run never erases a failure.

## Accepted prerequisite audits

[#51](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/51) was audited at
`4c76107097344fa9f27ffa6b0a3b74068f5ed396`, then merged normally as
`6a35ea968eedb06f06cf0615f8725777e4035256`. After integrating that main,
[#52](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/52) was audited at
`aec9564ea65bbca1a7a034d15a064166f575d951`, then merged normally as
`4279bd00eb08f125c6a4440b57f0091ce7c87df6`. Both merges have two verified parents,
trees equal to their audited heads, and a separate clean local main fast-forwarded to
origin/main. Only these existing PRs were authorized for automatic merging.

Both frozen snapshots passed Debug/Release builds, tests, native Windows/API34 and
external source consumers. #51 had 245 tests; #52 has 292 per configuration. #52's
integration tree equals the previously qualified `071d59d0fc26e573287718dd9007ba1eeda775df`.
Fresh integrated-head validation included Debug/Release tests, Windows, consumers and
another owned API34 AVD. Its initial Debug observation bound elapsed before the same
process subsequently emitted terminal PASS; that collection failure is retained.

| Exact-head CI gate | Successful run |
| --- | --- |
| #51 PR, rerun attempt 2 | [39146427487](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39146427487) |
| #51 main | [39287527391](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39287527391) |
| #52 integrated push | [39288648533](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39288648533) |
| #52 integrated PR | [39288659807](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39288659807) |
| #52 main | [39292222601](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39292222601) |

Every job/check, rather than only the run's top-level conclusion, was verified terminal.
#46 remains open. No package/version/tag/release was created and the main framework
checkout was not edited.

## Frozen source update

CurrentMaster is deliberately frozen to `2b7467c4443acfedfcff5a9e4eb42bfbeb8c6d31`,
observed 2026-10-19T16:36:22Z. Four native hosting seam files are byte-identical to the
previous `7cfa44e` snapshot. Relevant upstream changes include Page navigation, collection
lifecycle and Android Back routing. This snapshot receives separate qualification/CI;
HistoricalBaseline stays `69c5ea2`. This does not substitute for official 1.12 packages.

## Repeatable Android lanes

The driver requires an explicit serial and clean frozen framework root, audits the final
APK for **INTERNET only**, and records code/dirty state, APK SHA256, API, ABI, provider,
JDK, SDK, PID and a unique run marker. Physical installation requires prior human approval
and `-AllowPhysicalDevice`. It never resets a device, grants permissions, clears shared
logcat, restarts ADB or uninstalls another app. Nonincremental, nonstreaming installation
avoids an observed stuck incremental helper. The original failing logs remain available.

Two separate owned AVDs were prepared without changing existing AVDs: API23 Google APIs
x86_64 revision 33 and API34 Google APIs x86_64. An existing emulator lost its package
service and the fresh API34 AVD initially had an incomplete first boot; neither failure
was called a product pass. Only the owned new AVD was used for the subsequent fresh run.

```powershell
./scripts/test-android-runtime.ps1 -AdbPath $adb -Serial $api23Serial `
  -FrameworkRoot $framework -JavaSdkDirectory $jdk -Configuration Debug -Mode Floor -NativeBack -Recreate
./scripts/test-default-security.ps1 -Platform Android -AdbPath $adb `
  -Serial $api34Serial -FrameworkRoot $framework -JavaSdkDirectory $jdk -Configuration Release -Recreate
```

Repeat configurations after signing the matching sample. The test Activity sets only its
own KeepScreenOn flag. A phone must be manually unlocked; no keyguard/global setting is
changed. The approved NX769J has API36/arm64-v8a and Google WebView 155.0.8059.43. A first
sleeping-phone failure and subsequent complete unlocked runs are distinct evidence.
Historical APKs with implicitly merged storage/phone permissions are not installed there.

API23's stock Chromium 44.0.2403.119 failed the unchanged modern suite. The separate ES5
**Floor** lane positively executes main/same/opaque actors and verifies raw/local content,
actual DOM resources, CSP inline/eval denial, history, Reload, Stop, focus, JS settings,
five leases, unsupported nondefault scheme policy with Default retained, system Back
through the canonical MFN route to an explicit app GoBack, and actual Activity recreation.
Modern RPC prerequisites are false; Floor is not an RPC pass. One ES5 frame-control timeout
remains unexplained despite a later entire development Floor/Back/recreation PASS.
Final committed-head repeats failed in Debug at the frame control and in Release at
the public GoBack command after the second resource rendered. Thus API23 Floor remains
FAIL/PARTIAL, including a history qualification failure; no stable API23 claim is made.

Supply the actual build JDK with `-JavaSdkDirectory`; the native driver does not build
the APK or infer that a PATH JDK was used to compile it. An independently installed ADB
client can use `-AndroidSdkDirectory` to locate the pinned aapt2 build tool. No developer's
machine-specific JDK path is embedded in these scripts.

Modern lifecycle validation uses the framework's weak Activity tracker and retained Form.
A real RPC handler starts and remains pending while native renderer work starts, then
actual Activity.Recreate revokes the canonical document before destruction. A fresh
Activity/peer/document resets history and replays the collection/CSP/settings. The same
RPC client recovers only after explicit endpoint reinstallation. A real POST separately
triggers recreation and an explicit replacement collection. Server effects, arrival of
discarded SDK replies, process-death restoration and full Java retention are not inferred.

## Native recovery regression

The phone TLS fixture exposed an old error-page callback canceling script execution in
an immediately requested fresh raw document. Android now marks pending retirement before
failure observers run and uses its existing browser replacement path for the next explicit
navigation. Stop during loading records the same requirement; idle Stop alone does not.
Existing native generation guards revoke callbacks before View destruction. The host,
dispatcher and document identity remain canonical. Such fresh recovery resets history.
TLS rejection followed by real fresh-document script execution is the regression test;
the original failure is retained. This narrow fix does not qualify every history/reload
operation after a failed navigation.

## Windows and Visual Studio findings

Keyboard validation checks foreground PID before **each** SendInput batch and refuses
injection if ownership is lost. Executed portions cover native Tab/Shift+Tab in HTML,
HTML-to-MFN and Polish Unicode input. A native MFN-to-HTML Tab attempt timed out; another
run stopped at the foreground guard. Two-way input qualification remains PARTIAL/FAIL.
Unicode injection does not prove human IME composition.

The separate `--validate-desktop` lane moved the real sample across **100% (96 DPI)** and
**225% (216 DPI)** monitors; both moves preserved its peer and executable document.
No 150%/200% coverage is claimed. Maximize/minimize passed, but Normal after Maximize ->
Minimize left the HWND maximized (IsIconic=false, IsZoomed=true, peer unchanged). The
assertion remains FAIL. The main framework is read-only, so no workaround hides this.

The installed VS2026 Experimental Instance (18.10.3, Designer extension 1.10.0) actually
opened `eng/fixtures/Designer/WebViewPlaceholder.mfdesign` in its out-of-process host.
An inspected screenshot shows `browser` as a placeholder and an explicit diagnostic
that the external WebView type could not be safely created. The invalid SourceAddress
remains data. No engine appeared in the owned host's bounded process/module observation.
This is PARTIAL unknown-type fallback, not complete editable external metadata support
or a proof of no network. Only the owned VS/host were closed; no extension/settings changed.

## Default security and resources

Build `eng/fixtures/LoopbackSecurity` in both configurations before invoking
`scripts/test-default-security.ps1`. It owns random IPv4 loopback HTTP/TLS ports,
bounded counters/logs, a short-lived self-signed certificate and a three-minute lifetime.
The owner requests graceful stdin shutdown to dispose the temporary server key. Windows
Schannel's rejection of ephemeral keys initially broke this fixture; temporary default
key-set import corrected it without PersistKeySet, trust-store installation or a browser
validation callback. A typed Certificate failure, actual TLS connection, no HTTP request
through that rejected connection and fresh-document recovery are all required. Android
reverses only the owned TLS port with --no-rebind and removes that exact mapping.

Positive page actors verify default timer popup denial, geolocation denial and microphone/
camera NotAllowedError on tested modern providers. Unavailable APIs are unsupported, not
passed callbacks. Windows serves an actual harmless nonce-named attachment and verifies
its request plus bounded absence of that unique file in the real Downloads known folder.
This is not a general filesystem proof. Android has no equivalent trusted HTTPS attachment
fixture here; the sample continues denying cleartext traffic.

Existing 50-lease loops now record noncollecting managed/private/working-set observations
every ten leases; Activity tests also record bounded native/Java heap sizes. This does not
prove no leaks or complete Activity collection. Full heap/profile soak, human IME,
additional physical scales and trim/AOT remain separate qualification work. CI compiles
the fixture in Debug/Release and tests both frozen snapshots; hosted builds are not
represented as native device/foreground tests. IsOriginAuthenticated stays false and no
privileged C# handler or authentication capability is introduced.
