# Final audit and normal merges #41 → #42 → #43 (2026-10-18)

All three current diffs were reviewed against their actual bases: source, tests, native
fixtures, dependencies, workflow and documentation. No blocking defect was found in
their bounded deliveries. This accepts the RPC fixes and the research/strategy results;
it does not approve privileged messaging or interactive Blazor. #15 remains OPEN/PARTIAL;
#19's privileged portion and #17/#13/#18 remain blocked. #10/#22 stay open while the new
explicit-policy and research follow-ups are evaluated; no partial issue is auto-closed.

## Exact reviewed heads and merge integrity

| PR | Final reviewed head | Terminal successful CI before merge | Normal merge commit |
| --- | --- | --- | --- |
| [#41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41) | `1facdc2974f2e060a3b32ba5bd6cfefacbc6a624` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124173180), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39124170315), all shared/Windows/Android SUCCESS | `33fb2433d4d6d8c6687ded65a7c5b2f7747fefac` |
| [#42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42) | `a51aa1e954f814b0b7adb9d9090a50c0cddeeaa2` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39130872830), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39130869475), all SUCCESS | `5ba66a5a4039d2a3f677ef2107acd56f9115b0cf` |
| [#43](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/43) | `3d9ecfff71f341624c68e5ed8a1033752d486e18` | [PR](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39131727154), [push](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39131722811), all SUCCESS | `b9a5c5e918cc549c0349dbbb13c144db2e7d0deb` |

Each merge has two parents and its tree equals its reviewed head. After each merge local
main was fast-forwarded. #42 and #43 were retargeted to main, ordinarily integrated with
that main, pushed and checked on their **new** heads before merge. Their trees equal
original `17e3c8a4fbfc94563d25a2e88afc62ea6a8c16f4` and
`514ff02c9ca0bcb5b0c2ce6773fc8230e0428500`, respectively. No squash, rebase, force push,
branch-protection override or merge of an unverified head.

Post-merge main runs: [after #41](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39130863516),
[after #42](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39131714947),
[after #43](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39132381296).
All three post-merge runs reached terminal shared/Windows/Android SUCCESS, including
the exact current main `b9a5c5e918cc549c0349dbbb13c144db2e7d0deb`.

## Code, API and security findings

- #41: the existing total deadline includes synchronous JSON conversion; monotonic checks
  prevent expired submission/return and forward the remaining page budget. Cancellation
  wins coincident conversion/native/transport failures. Pending entries and linked/timer
  sources are released in finally. UI entry/disposal and off-thread response conversion
  rules are documented. No new exception/cancellation system, protocol version or handler.
- #42: public .NET10 WebViewManager/HtmlRenderer APIs, actual SDK-compiled Razor, isolated
  scoped DI and disposal, native controls and the canonical MFN dispatcher. NuGet JS and
  notices are embedded/copied unchanged. ASP.NET references are research-only and outside
  the product solution. Protected inbound MessageReceived is never called; no Blazor page
  attaches. Native CSP denial is positively observed. Passive rendering is not interactivity.
- #43: documentation-only strategy; whole-application trust differs from original-script
  identity. World APIs are documented as unqualified alternatives, with DOM/relay/lifetime
  gates. Source, IDs and nonces remain correlation/context evidence, never authority.
  Android AddJavascriptInterface remains unprivileged. No product policy/API changes.
- Native resource ownership, callbacks/site currency and canonical request cancellation
  remain authoritative. No second host/layout/focus/dispatcher, main-framework/pin change,
  package metadata/version change or publication.

## Fresh local qualification

For **each** final reviewed head, full restore and sequential Debug/Release solution
build/test: **207/207 each**, zero warnings/errors/skips (158 shared, 37 Windows, 12 Android).
Windows and ordinary Android native validators: **RUNTIME PASS in both configurations**.
#42 additionally reruns the Razor build/contract/native/profile-cleanup probe and AndroidX
full matrix with Activity recreation in both configurations: PASS, retaining intentional
interactive/original-caller NO-GO. #43 changes only docs; those unchanged research sources
reuse the explicitly identified fresh #42 evidence, rather than claiming another run.

Commands: pinned bootstrap; solution restore, build `-c Debug/Release --no-restore -m:1
-p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:EnableWindowsTargeting=true`,
tests `--no-build --no-restore -m:1`, all with external ModernFormsNextSourceRoot; Windows
`--validate`; Android SignAndroidPackage/JDK21, explicit `adb -s emulator-5554` install and
validation Intent/PID-filtered terminal log. Research commands are in their READMEs.
Ignored evidence: artifacts/audit41-new, audit42-new, audit42-blazor, audit42-origin,
audit43-new. Windows SDK1.0.4258.31/runtime155.0.4283.62; Android API34 x86_64/provider113.
Hosted CI is separate from these local native executions. No timeouts/skips/warnings or
security policy were weakened. No new manual cleanup attempt on historical Temp leftovers.

NOT EXECUTED/UNQUALIFIED: API23 runtime, physical device, genuinely unsupported provider,
foreground keyboard/UIA/manual visual, interactive Designer, full IME/DPI/accessibility,
retained heap/Activity soak, in-flight product RPC across Android Activity recreation,
interactive Blazor/privileged bridge/world security, full trimming/NativeAOT runtime.
The earlier publish diagnostics remain historical evidence; no new publish run is claimed.
