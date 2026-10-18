# #19 — errors, deadlines and cancellation

## Implemented scope

This extends the existing WebView, WebViewTransport and WebViewRpcClient contracts.
There is no second exception/cancellation system, privileged registry or native host.
RPC still invokes only page JavaScript and accepts untrusted results. Existing error
categories remain compatible; errors omit payloads and native/page exception details.

Audit reproduced ten failing regression cases before the fixes:

1. The declared total deadline started after request serialization. A slow converter
   could exceed it and still submit, or report InvalidPayload after its deadline.
   The page received the original timeout instead of the remaining budget.
2. A successful converter could reenter/cancel and still reach the transport method.
3. Coincident transport failures won over caller/client/transport/document cancellation.
4. WebView typed/untyped native failures likewise won over requested cancellation.

The existing deadline source starts before conversion. Monotonic elapsed checks handle
synchronous converters and delayed timer dispatch; expired serialization never submits.
The page receives the remaining bounded budget. Native effects already submitted cannot
be undone; synchronous converters cannot be preempted. Keep converters short; response
conversion may occur off-thread and must not access UI.

Cancellation is rechecked before submission, conversion return and error mapping. It
takes precedence over coincident failures. Existing Timeout applies to expiry only when
no caller/document/transport/client cancellation occurred. InvalidPayload, InvalidResponse,
Transport and RemoteError remain sanitized. Pending registrations are removed if acquired;
deadline/linked sources are disposed on every path. Advisory cancel stays scoped to the
original current document. No retry, replay or implicit endpoint installation occurs.

Raw ExecuteScriptAsync keeps native JSON/null exception projection; no eval wrapper
changes its scope. Explicit RPC failures retain fixed allowlisted page error codes.

## Validation design

- Red-before/green-after cases reproduce the actual defects. Further cases exercise
  capacity after repeated InvalidResponse and the canonical WebView → WebViewTransport
  → RPC path through navigation, Stop, JS disable, host loss, browser/transport/client
  disposal and fresh-host reinitialization.
- A retired peer's original response cannot complete a replacement call. The headless
  host remains alive and drains its existing dispatcher until conversion settles;
  early cleanup would correctly cancel even the new call. No blocking Task wait or
  analyzer suppression is used.
- Both native validators check expired serialization causes zero page-handler calls,
  typed conversion errors, rejected Promise, independent parallel cancellation/errors,
  Stop, JS disable, explicit reload/reinstallation and borrowed wrapper disposal.
- Windows also cancels outstanding RPC during real cross-window host retirement, then
  reuses the same client after explicit endpoint installation.

The baseline retains readiness/JSON behavior. Configure JS before load; after changing
it, reload before reinstalling the endpoint. Application converter execution and native
side effects are not rollbackable.

## Boundaries

[The #39/#40 merge audit](pr39-40-final-audit.md) records prerequisite acceptance, heads,
fresh native checks and CI. Privileged handler errors/cancellation remain BLOCKED by
#15/#17/#13. AuthenticatedMessaging is absent; IsOriginAuthenticated remains false.

Full Debug/Release and exact-head CI results are recorded with delivery. Commands remain
in [testing](testing.md). Windows WebView2 155 and Android API34/provider113 execution
does not establish API23 runtime, physical-device, human visual/UIA, full IME/DPI/
accessibility/heap soak or NativeAOT/full trimming. Android Activity recreation with an
in-flight product RPC is unexecuted; headless host and Windows real host replacement are
separate evidence lanes. No framework/pin/template/package metadata change or release.

## Completed local validation

Full restore, sequential full Debug/Release solution builds: **0 warnings/errors**.
All tests: **207/207 each**, no skips (158 shared, 37 Windows, 12 Android policy).
Windows WebView2 and Android signed APK/emulator validators: **RUNTIME PASS in both**.
Both execute the new RPC failure matrix; Windows additionally passes real pending-RPC
host retirement/recovery. No analyzer suppression or test timeout relaxation was used.

19 new deterministic cases supplement prior coverage; ten reproduced the defects on
the original code before the fixes and then passed. A new host-recovery fixture initially
disposed its owner before asynchronous conversion completed; it was corrected to drain
the canonical test dispatcher while that owner is still alive, preserving cancellation
priority. Product logic was not weakened to satisfy the fixture.

Commands: solution restore; `dotnet build -c Debug/Release --no-restore -m:1
-p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false`; `dotnet test --no-build
--no-restore -m:1`, all with the external pinned source root; Windows sample `--validate`;
Android `-t:SignAndroidPackage`, JDK21, explicit `adb -s emulator-5554` validation Intent
and PID-filtered logs. Evidence: ignored `artifacts/issue19/`, plus red/green diagnostic
logs. Release completed on 2026-10-18 after an overnight execution gap; the reported
build wall time is not a performance qualification. Final tests/native runs completed
afterwards, with the same recorded engine/provider versions.

The separate AndroidX probe sources/JavaScript are unchanged; its fresh #39 native
qualification is recorded separately rather than relabeled as a new #19 probe run.
Hosted exact delivery-head SHA/jobs and PR are recorded with delivery. The new PR stays
open; privileged handler work remains blocked and no release is created.

Delivered in open [PR #41](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/41),
implementation `68c97947f548910d335c07b6b69dc177af24c340`, based on merged main `b329da3`.
Delivery metadata changes documentation only; exact final-head SHA and terminal hosted
shared/Windows/Android results are recorded on the PR after completion.
The main framework advanced externally to `1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1`
during this work; it was read-only here. Diff inspection found no changes from the approved
pin in NativeViewHost, NativeViewHosting or either typed native-view site. This is contract
inspection, not a full build against that newer framework head. The approved pin is unchanged.
