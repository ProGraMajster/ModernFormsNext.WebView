# Closed native program and one-use diagnostic permit

This separate Windows research project implements the owner's bounded experiment:
`research/ClosedProgramProbe.Windows`. It is not packaged or added to the product
solution. Product code, public APIs, capabilities, transport and framework pins are
unchanged. It source-links the **exact** existing `WindowsWorldProbe/ResearchPeer.cs`,
product `NavigationCommandQueue.cs` and loopback fixture. NativeViewHost, the real
dispatcher and original peer/document cancellation remain authoritative.

## Audited call graph and authority

1. A finite native harness action calls `GrantCounterIncrement`, selecting only
   `IncrementOwnedCounter(delta=1)`, with a 10-second monotonic bound (40ms in the
   explicit expiry negative). No page selects a method, argument, result or service.
2. The permit captures the current canonical document/token and actual native context
   description. The session already owns the exact ResearchPeer, Core event channel
   and transport. A replacement peer cannot use that receiver.
3. The **entire authorized JavaScript program** is `void __mfnClosedCounterBinding("...")`.
   C# serializes a primitive literal with the exact fixed operation, delta and fresh
   permit nonce. It reads no DOM, window property, event, message, page-controlled
   object/prototype, JSON object or callback. It installs no listeners.
4. Only the private `Runtime.bindingCalled` SDK event handler can call
   `OwnedCounter.IncrementOne`. It checks native context, canonical lifetime, exact
   permit payload and monotonic expiry, clearing the permit **before** the effect.
   One shared counter is capped at128 across all retired/fresh sessions.

The public/raw page channel only collects attack evidence. It cannot enter the native
receiver, create a permit, select an operation, return privileged data or mutate the
counter. Actor completion never supplies grant parameters. All grants are finite
native harness call sites; proof may advance an assertion, but the next operation is
a separately predetermined diagnostic action. No service discovery, DI, files,
reflection, host objects or public Core exposure exist. The native host is trusted:
this diagnostic is not a general RPC service or authentication of an original caller.

## Native context and failure behavior

`Runtime.executionContextCreated` supplies id, uniqueId and auxiliary frameId,
isDefault/type. `Page.getFrameTree` supplies the actual main frame. The authorized
context must match that exact frame, type=isolated, isDefault=false and a nonempty
uniqueId. Descriptions are bounded to64, callbacks to256, packets to2048 characters.
Native context destruction/clearing revokes permits. Missing metadata, unsupported
methods or failed execution end as NO-GO, with no main-world fallback.

`Runtime.addBinding.executionContextName` selects SDK worlds, **not** authority.
Names are constant and disclosed. A real same-named isolated child-frame world gets
the binding and is denied with the exact valid payload. URL/origin/ID/label/nonce
alone cannot authorize this operation.

Native execution uses experimental `Runtime.evaluate.uniqueContextId` to avoid
submission to reused numeric IDs. An actual invalid-unique-ID `void 0` must be
rejected by the SDK; ignoring the field means NO-GO. Positive callbacks must also
match native metadata. Binding events expose only numeric context IDs: cancellation,
destroy/clear revocation, exact native ledger identity and one-use fresh payload
remain necessary. Numeric IDs are never process-global identity.

The old Core is captured solely for **actual stale SDK execution attempts**. Refusal
is never renamed live delivery. `InspectRecorded` computes a reason with no mutation
or consumption path; recorded packets never enter the live handler. Canonical tokens
cancel permits before SDK Stop/retirement. Sequential sessions remove their binding
and disable Runtime so the next session obtains fresh descriptions. A retired session
never removes anything from a replacement Core.

## Executed attacks and controls

HTML-parsed main, same-origin iframe and sandbox/opaque iframe actors execute direct
and parent binding/helper, DOM dataset/event, postMessage relay, prototype pollution,
named-property clobber and forged-CDP-JSON attempts. Full current payload/name/context
data is disclosed. Each actor has an eight-step proof, actual raw delivery of its
forged payload, zero unauthorized effect, a genuine wrong-payload SDK callback fence
and a fresh native positive control. Opaque parent access is denied; same-origin
parent access executes. Proofs are untrusted fixture instrumentation, never caller
authentication. The closed authorized program consumes none of their values.

| Probe | Required observation |
|---|---|
| No permit | Real correct-world callback denied, counter unchanged |
| Main/same/opaque actors | Independent execution/forged raw delivery, zero binding effect, fresh control |
| Same-name child isolated world | Real exact-payload SDK callback denied by native context, fresh control |
| Wrong method/delta/nonce | Real callbacks denied, fresh control after each |
| Expiry | Monotonic expiry rejection from real callback, fresh control |
| Consumed payload / old payload under new permit | Real replay callbacks denied, fresh control |
| Recorded packet | Explicit inspection, no mutation or native-channel promotion |
| Stop | Token already canceled, surviving-world live callback rejected, separate recorded inspection, fresh reload control |
| JS disable/re-enable | Canonical revocation, real SDK attempt, live rejection versus refusal distinguished, fresh document control |
| Cross-origin HTTP302 / same-URI Reload | Actual navigation, revocation, old-context SDK attempt, recorded inspection, fresh control |
| Cross-window peer recreation | Old peer noncurrent/generation advanced, actual old Core attempt, fresh peer/world control |
| Dispose / replacement | Original cancellation, old SDK attempt, same counter unchanged, fresh replacement control |

Development Debug on Windows26300/.NET10.0.401/WebView2 **155.0.4283.62** reached narrow
diagnostic GO with zero unauthorized effects. The final matrix expects **19 authorized
increments**, including separate fresh controls for both replay negatives. Stop and
JS-disable/re-enable produced real rejected callbacks. Reload, actual cross-origin302,
cross-window recreation and Dispose produced SDK execution refusals, not callback
claims. Actual unique IDs differed across peers; logs do not assume numeric collisions.
Exact clean-head Debug/Release qualification and CI belong in the PR, separately
from this development observation. No wider realm/engine guarantee is inferred.

## Reproduce and decision

```powershell
dotnet build research/ClosedProgramProbe.Windows -c Debug -m:1 -warnaserror `
  -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false `
  -p:ModernFormsNextSourceRoot=<clean-frozen-framework-root>
./research/ClosedProgramProbe.Windows/Validate.ps1 -Configuration Debug `
  -FrameworkRoot <clean-frozen-framework-root> -Snapshot CurrentMaster
# Repeat Release and HistoricalBaseline with its matching frozen root.
```

The validator uses a unique owned temporary profile, actual process-tree exit and
the existing 20x100ms deletion bound. It checks the absolute target before recursive
cleanup and records head/dirty/framework/SHA256/configuration/result. CI compiles
Debug/Release for both frozen snapshots; a hosted build is not native execution.
Product/source-consumer/native regressions and the previous deputy counterexample
remain independent requirements.

**Narrow diagnostic GO for the measured closed native program plus one-use permit
only. General Hybrid / original-caller authentication / Blazor AttachPage: NO-GO.**
The earlier [DOM deputy counterexample](windows-world-experiment.md) remains valid:
an isolated-world listener reading page-controlled DOM can execute attacker intent.
Adding a listener, general dispatcher, async page callback or services invalidates
this closed call graph and requires another independent audit.

No product handlers are enabled. IsOriginAuthenticated stays false, AuthenticatedMessaging
unavailable; #15 stays OPEN/PARTIAL, #13/#17/#18 gated. This is not CSP qualification:
hostile HTML intentionally has no CSP. No universal access, CSP bypass, debugging
port or privileged product API is added.

Android worlds are **NOT IMPLEMENTED / NOT QUALIFIED**. Baseline API23/dependencies
stay unchanged. A separate AndroidX candidate needs actual API/provider detection
(newer AndroidX versions may require API24+), canonical JNI/lifecycle and independent
physical-device proof. An OS/process-isolated trusted channel is another future
candidate. Neither AddJavascriptInterface nor the popup port authenticates a caller.

Primary definitions: [Runtime schema](https://github.com/ChromeDevTools/devtools-protocol/blob/master/json/js_protocol.json),
[Page schema](https://github.com/ChromeDevTools/devtools-protocol/blob/master/json/browser_protocol.json),
[WebView2 CDP event channel](https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.getdevtoolsprotocoleventreceiver).
Behavior is probed on the installed engine rather than inferred from the moving schema.
