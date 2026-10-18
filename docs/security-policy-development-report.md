# Security policy, Blazor and baseline report — 2026-10-18

This is the **original delivery snapshot**. Subsequent owner-authorized audits accepted
#47/#48/#49 as normal merges ba7260a, a98573c and 6c4eb9c. Their original heads/CI below
remain historical evidence; they are no longer open deliveries. The live #50 PR records
its final integrated head/audit. Fresh current-master source qualification uses
1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1, separately from historical baseline 69c5ea2.
The owner's binding release decision is NativeViewHost in ModernFormsNext 1.12.0:
missing 1.11.1 contracts are an expected historical negative control. Source development
continues; package alignment waits for actual official 1.12.0 and release qualification.

## Authorized audit and merges

Complete diffs, API/threading/cancellation/resources, native evidence, research dependency
isolation and GO/NO-GO claims were reviewed. No blocker remained. Each exact-head merge
used an ordinary two-parent commit, tree equality and local main fast-forward.

| PR | Reviewed HEAD | Normal merge commit |
| --- | --- | --- |
| [41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41) | 1facdc2974f2e060a3b32ba5bd6cfefacbc6a624 | 33fb2433d4d6d8c6687ded65a7c5b2f7747fefac |
| [42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42) | a51aa1e954f814b0b7adb9d9090a50c0cddeeaa2 | 5ba66a5a4039d2a3f677ef2107acd56f9115b0cf |
| [43](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/43) | 3d9ecfff71f341624c68e5ed8a1033752d486e18 | b9a5c5e918cc549c0349dbbb13c144db2e7d0deb |

All exact-head jobs and main CI after each merge succeeded. Each audit ran fresh
Debug/Release full builds,207/207 tests each, zero warnings/errors/skips and native
Windows/Android PASS both configurations. #42 also ran default Blazor and AndroidX
origin/Activity-recreation research Debug/Release; #43 explicitly reused that unchanged
research evidence. Accepting NO-GO research did not enable interactivity.
[Detailed audit and CI/merge proof](https://github.com/ProGraMajster/ModernFormsNext.WebView/blob/140fafe201dac789c1351a629836983f603d64c1/docs/pr41-43-final-audit.md).

## Original delivery heads and CI

| Stage / branch | PR / final HEAD | Exact-head CI: all jobs terminal SUCCESS |
| --- | --- | --- |
| Policy / work/local-content-security-policy | [47](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/47),140fafe201dac789c1351a629836983f603d64c1 | [39135940341](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39135940341),[39135936505](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39135936505) |
| Blazor / work/blazor-application-policy | [48](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/48),93505cccdcf36e4adcb8f0f78475df24c86ca8c7 | [39136623078](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39136623078),[39136619690](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39136619690) |
| Worlds / work/windows-world-probe | [49](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/49),c87bd6e5d3ffc8ec896eedb590c0c2a2c02c2064 | [39137803285](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39137803285),[39137799416](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39137799416) |

#48 was originally stacked on verified #47; #49 on verified #48. Previous local validation and
terminal exact-head CI passed before each next stage. This baseline-readiness branch
starts independently from main b9a5c5e; its own delivery HEAD/CI are recorded on its PR.
At that original delivery none was merged. The subsequent accepted merges are identified
above; this sentence is not the current PR status.

## Implemented policy and risk

Duplicate audit covered open/closed issues. New
[#44 API](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/44),
[#45 qualification](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/45)
and [#46 deferred platform controls](https://github.com/ProGraMajster/ModernFormsNext.WebView/issues/46)
reuse existing #14/#32.

PR47 adds typed None/Self selectors, immutable WebViewLocalContentPolicy, an additive
catalog constructor/Policy property, optional IWebViewLocalContentPolicyPeer and
ConfigurableLocalContent. The original constructor/exact default CSP are unchanged.
Unsupported nondefault policy fails before native submission, including legacy
ready/queued/recreated peers. No raw CSP/origin strings, global switch or TLS bypass.

| Selector | Default | Application | Effect / risk |
| --- | --- | --- | --- |
| Scripts | Self | Self | Origin external scripts; None denies; inline/eval remain denied |
| Styles | Self | Self | Origin stylesheets; None denies; inline stays denied |
| Images | Self | Self | Public origin images; None denies; no remote/data source |
| Connections | None | Self | Same-origin browser connections; no endpoint/CORS grant or firewall |
| BaseUri | None | Self | Same-origin base paths change relative resolution; no foreign origin/port |
| Existing IsJavaScriptEnabled | False | Unchanged | Separate per-WebView setting; policy does not enable JS |

Select immutable public bytes/policy before navigation; a new catalog/document revokes
old canonical operations. Header delivery support is feature-detected. Actual CSP effects
are separately qualified on Windows155 and Android113/API34; API23 runtime is unqualified.
After a denied foreign base, Android can retain its prior valid self base while Windows
falls back to document URI. Both deny the foreign origin/port. Frame resource restrictions
do not prove absence of inherited srcdoc/about:blank DOM contexts. Native ExecuteScript
remains a separate explicitly authorized engine operation.

Both samples include clean local fetch opt-in examples. XML docs, compatibility/lifecycle
tests and the full option/effect/risk/support table are in the
[policy ADR](https://github.com/ProGraMajster/ModernFormsNext.WebView/blob/140fafe201dac789c1351a629836983f603d64c1/docs/local-content-policy.md).
Broader platform settings remain deferred.

## Blazor and world decisions

PR48 uses original ASP.NET10.0.12 assets and public WebViewManager with the existing
MFN host/dispatcher. Application mode gets the real manifest (200/JSON array/exact CSP),
correct origin base and resolved original Blazor.start(). Real AttachPage is observed
before a forged request, then rejected; TryDispatchAsync stays false. **A assets GO;
B authorized IPC/interactivity NO-GO.** Static Razor/scoped DI disposal/native snapshot
buttons are not an interactive Razor circuit. Default mode retains actual CSP denials.
[Blazor ADR](https://github.com/ProGraMajster/ModernFormsNext.WebView/blob/93505cccdcf36e4adcb8f0f78475df24c86ca8c7/docs/blazor-application-policy.md).

PR49's internal SDK fixture reuses NativeViewHost/typed site/dispatcher, canonical client
IDs/transport tokens and the product acknowledgement queue. Real page/same-origin/opaque
iframe actors cannot directly call its named world binding. Nevertheless, an isolated
DOM listener forwards page/iframe-selected commands into a bounded diagnostic counter:
**native world identity is insufficient for operation authorization**. Tests include
real Stop callback denial, HTTP302 origin change, reload, cross-window recreation and
Dispose, plus explicitly labelled recorded-packet guard checks and fresh-world controls.

No C# operation/DI/file/reflection handler is attached. Hostile raw fixtures have no CSP;
production policy is unchanged. World CSP inheritance, Android worlds and whole-app
trusted realm are NOT QUALIFIED. No universal access, setBypassCSP, remote debugging or
all-context fallback.
[World ADR](https://github.com/ProGraMajster/ModernFormsNext.WebView/blob/c87bd6e5d3ffc8ec896eedb590c0c2a2c02c2064/docs/windows-world-experiment.md).

IsOriginAuthenticated remains false; AuthenticatedMessaging remains absent. Source,
request IDs, tokens, CSP and Android AddJavascriptInterface do not authenticate the
original caller. #15 stays PARTIAL/OPEN; #17/#13/#18 BLOCKED/OPEN. #19/#22/#10 were not
falsely closed. Interactive Blazor/#23–#31 remains NO-GO.

## Validation and limits

Each policy/Blazor/world stage passed full restore, sequential Debug/Release solution
builds with zero warnings/errors,245/245 tests each (196 shared,37 Windows,12 Android;
zero skipped), and native Windows/Android both configurations. Blazor default/application
and Windows world research also passed their own Debug/Release build/native matrices and
profile cleanup. Hosted contracts/builds are separate from local native UI evidence.

Commands: bootstrap-framework.ps1; dotnet restore/build/test on ModernFormsNext.WebView.slnx
with -m:1, UseSharedCompilation=false, GeneratePackageOnBuild=false and explicit source
root; Android SignAndroidPackage/JDK21 and adb **-s emulator-5554**; research Validate.ps1
commands from its READMEs. SDK10.0.401; WebView2 SDK1.0.4258.31/runtime155.0.4283.62;
Android API34 x86_64/provider113.0.5672.136. No physical phone was touched.

NOT EXECUTED: API23/physical device, foreground keyboard/UIA/human visual, Designer/full
IME/DPI/accessibility, sustained heap/performance and new full trim/NativeAOT. API34 is
not API23 evidence. Application-policy product Activity recreation is not claimed;
AndroidX research recreation does not prove it.

Red fixture diagnostics are retained. Corrections tested eval in loaded page script,
safe engine base-path differences, real navigation cancellation/typed failure/reload,
SDK exception projection, nullable raw Reload Source and actual cross-window recreation.
No security policy, warning, timeout, skip or threshold was weakened.

## Independent baseline route

The [preview/stable checklist](baseline-preview-readiness.md) and official-package probe
track the expected 1.11.1 negative control and future official 1.12.0 alignment independently
of Hybrid. The original readiness contribution changes
only readiness tooling/docs. No product package or release was produced/published.
Next: develop on a recorded current-master source snapshot; qualify baseline API23/physical/
manual lanes; after official 1.12.0, align packages and review a concrete candidate
before publication approval. Privileged Hybrid/Blazor retain separate trust/IPC gates.

The main framework remained read only, source pin unchanged and pre-existing local
configuration preserved.

Original validation on the independent b9a5c5e-based branch: full restore/sequential
Debug/Release builds with zero warnings/errors,207/207 tests each (158 shared,37 Windows,
12 Android), zero skipped, and native Windows/Android PASS both configurations.
Official1.11.1 control consumers compile Debug/Release; required hosting consumers fail
both with the three missing public types. Android's package index is HTTP404.
This is an expected negative1.11.1 control, not a defect blocking source development.
Preview remains NOT READY until actual official 1.12.0 and release qualification.
Exact package hashes and the preview/stable checklist are in the linked readiness report.
