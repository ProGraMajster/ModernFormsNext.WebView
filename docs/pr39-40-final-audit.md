# Final audit and normal merges #39 → #40 — 2026-10-17

All source, dependency/workflow, test/sample and documentation diffs were reviewed
against actual bases and issues. No blocker was found in these bounded deliveries.
This accepts AndroidX research and Windows correlation hardening; it does not accept
privileged original-caller authentication. Issue #15 remains OPEN/PARTIAL; #16 retains
its explicit privileged NO-GO and qualification limits.

## Review

- #39/#16: isolated SDK APK, exact origin/direct-frame metadata, hostile parent-object
  relay, feature rejection, replies, redirect/reload, listener/proxy cleanup and Activity
  recreation. AndroidX is research-only; ordinary product resolves zero AndroidX packages.
  API23 manifest declaration is not runtime evidence. Metadata does not identify every
  original script that can invoke an object owned by another same-origin frame.
- #40/#15: existing Windows peer/private packet guard, parsed local native Source,
  request-bound native token, stale-ID substitution/revocation and async helper/JS-toggle
  race. Public helper/strings/transport/RPC remain compatible. Same-origin parent-helper
  calls remain untrusted. No C# handler or product authentication capability is exposed.
- Ownership stays in the framework host, native command queue and document tokens.
  No second host/layout/focus/dispatcher, product AndroidX dependency, source-pin change,
  public removal, warning suppression or policy relaxation was introduced.

## Exact heads and history

| PR | Final reviewed head | Terminal successful CI | Normal merge |
| --- | --- | --- | --- |
| [#39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39) | `96becb8a4692b9b9de0086c377b05521f6653d7c` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050287675), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39050284966) | `9ebbfd9e059f0665cccbd94e4e574f74b9e89ce5` |
| [#40](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/40) | `577d3d45a10ea4718b04c411b47f29e6ebe74cbe` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053261390), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053258167) | `b329da3eb114f9cd713b60feffe7242b22c0f0e1` |

After #39, local main was fast-forwarded and #40 retargeted to main. A regular integration
merge incorporated main, producing #40's new head above. Its tree equals previously
reviewed `030b71e950274f2915045ded6a8f7c4399a228cd`; fresh local checks and all three
exact-head CI jobs passed before merge. No squash, rebase or force push.
Each merge has two parents and first-parent order is #39 then #40. Merge trees equal
reviewed heads. Local main = origin/main = `b329da3eb114f9cd713b60feffe7242b22c0f0e1`.
Post-merge CI SUCCESS: [after #39](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053252263),
[after #40](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39053637461).
Issue #15 was rechecked OPEN after merge. No new issue is closed by this audit.

## Fresh validation

| Tree | Debug | Release | Native |
| --- | --- | --- | --- |
| #39 exact head | Full build 0 warnings/errors; 166/166 tests | Same; 166/166 | Windows/Android RUNTIME PASS both; research full matrix/recreation PASS both |
| #40 final head | Full build 0 warnings/errors; 188/188 tests | Same; 188/188 | Windows/Android RUNTIME PASS both, actual packet substitution and JS Starting race |

No skips. #39 split: 139 shared/15 Windows/12 Android; #40: 139/37/12. Pin stays
`69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`; SDK10.0.401. Commands: restore; full solution
Debug/Release build sequentially, `-m:1 -p:UseSharedCompilation=false
-p:GeneratePackageOnBuild=false`; tests `--no-build --no-restore`; Windows `--validate`;
ordinary Android signed APK/validation Intent; research signed APK/explicit-serial
`Validate.ps1 -Recreate` each configuration. JDK21, only emulator-5554, PID-filtered logs.
Evidence: ignored `artifacts/audit39/` and `artifacts/audit40/`. No source repair was needed.

Windows runtime155.0.4283.60; Android API34 x86_64/provider com.google.android.webview113.
Research Release is Mono AOT/partial trimming, not NativeAOT/full trim. Research native
execution was refreshed on #39; #40 changes Windows/docs, not the Android probe.
NOT EXECUTED: API23 runtime, physical device, genuine unsupported provider, foreground
keyboard, interactive Designer, other runtime/OS floors, full IME/DPI/accessibility/
retained-heap soak or privileged bridge. Native logs are not human visual/UIA evidence.
Main framework/templates are untouched; no packages, tags or releases were published.

Next: #19 canonical error/cancellation gaps, #22 bounded public-API feasibility and a
separate Hybrid trust strategy. Privileged handlers remain gated throughout.
