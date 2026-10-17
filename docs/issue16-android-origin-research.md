# #16 — AndroidX origin/frame research and ADR (2026-10-17)

Decision: **GO for the bounded native-metadata prototype; NO-GO for privileged original-caller
authentication using the tested main-world object.** No production authentication capability,
C# registry or AddJavascriptInterface fallback is enabled. #17 remains gated.

## Scope and components

Branch `work/issue-16-android-origin-research` starts from merged main
`f4017e7be66eeebf235e9e296caf1e5e7855ad19`, after #36/#37/#38 passed exact-head CI and
normal merges. [Stage 1 audit](pr36-38-final-audit.md) records their commits and acceptance.
Issues #33/#11/#12 are closed for their scoped criteria, independently of authentication.

`research/AndroidOriginProbe` is an isolated native SDK test APK, outside the product solution,
with its own package ID. Its Activity owns a fresh SDK View; it is not another product host,
adapter, transport or dispatcher. Production providers and baseline sample references are
unchanged. The research APK is the sole AndroidX consumer. CI separately builds/signs it;
that does not constitute an emulator run. Components include the project/manifest, SDK
fixture, explicit-serial validator, pinned binding, CI build and this ADR/report.

## Dependency selection

Official NuGet archives, nuspecs, .NET assemblies and embedded AAR manifests were inspected.
No dependency is vendored and no framework pin is changed.

| Microsoft binding | .NET10 Android36 asset | AAR minimum | Decision |
| --- | --- | --- | --- |
| Xamarin.AndroidX.WebKit 1.17.0 | Present | API24 | Reject for API23 branch |
| Xamarin.AndroidX.WebKit 1.16.0.1 | Present | API24 | Reject for API23 branch |
| Xamarin.AndroidX.WebKit 1.15.0.2 / WebKit1.15.0 | Present | API23 | Pin for isolated probe |

Selected nuspec dependencies: AndroidX Core >=1.17.0.2, Annotation >=1.9.1.7,
Annotation.Experimental >=1.5.1.3, JSpecify >=1.0.0.6. Resolved dependencies and the merged APK
manifest must also preserve API23; a primary AAR check alone is insufficient. The binding
license is MIT AND Apache-2.0; its package carries licenses/notices. This adds no AndroidX
reference or minimum-version change to ordinary WebView.

## API evidence and threat boundary

[WebViewCompat](https://developer.android.com/reference/androidx/webkit/WebViewCompat)
injects into matching frames. The probe uses one exact HTTPS origin, no wildcard, registers
before load and checks WEB_MESSAGE_LISTENER on the installed provider. A compatible OS and
binding do not guarantee provider support. The forced unsupported branch skips registration
and verifies no object is injected; no less restrictive fallback is attempted.

[Native listener metadata](https://developer.android.com/reference/androidx/webkit/WebViewCompat.WebMessageListener)
and [reply proxies](https://developer.android.com/reference/androidx/webkit/JavaScriptReplyProxy)
describe the injected object's frame/context. They do not identify every script which obtained
a reference to that object.

**Observed counterexample on API34/provider113:** a same-origin iframe invokes
`parent.originProbe.postMessage(...)`; the callback reports the parent's allowed origin and
`isMainFrame=true`. Direct use of the child's object reports `isMainFrame=false`.
Thus allowedOriginRules/sourceOrigin/isMainFrame cannot enforce rejection of every original
subframe caller. This is a measured limitation of that policy, not a claim that all AndroidX
uses are insecure. Direct-frame metadata and cross-origin exclusion still work as tested.

The only native response is a fixed public acknowledgement; RPC coverage is JS echo.
There is no privileged action, CLR/service/file exposure or C# method dispatch. The baseline
AuthenticatedMessaging capability remains absent. Origin, feature support, IDs and reply
proxies never upgrade existing transport/RPC data to authenticated commands.

## Native matrix and lifecycle

The interceptor serves fixed public adversarial HTTPS fixtures at two private test origins
and a mismatched port. They are separate from WebViewLocalContent: production CSP remains
unchanged. No cleartext, TLS, file/content-access or permission policy is relaxed.

| Case | Evidence |
| --- | --- |
| Exact HTTPS top-level | Native origin/main metadata and fixed reply-proxy acknowledgement |
| Same-origin direct iframe | Native child-frame metadata |
| Same-origin parent-object call | Parent metadata from child script: privileged NO-GO |
| Foreign HTTPS iframe / other port / opaque srcdoc | Positive execution fences; no injected object/message |
| Existing RPC v1 | Production JS endpoint and Android escaping; public echo result42 |
| Remove/re-register | Revoked listener identity; removed callback excluded |
| Replacement / old reply proxy | Old proxy does not deliver into replacement; fresh fence executes |
| Cross-origin meta redirect | Destination has no injected object |
| Reload and Stop/removal | Reload object works; listener removed at Stop boundary |
| Activity recreation | Full probe repeats with new Activity/View; old native fixture disposed |

Retained packets are bounded to256 and UTF-8 input to the RPC limit. Activity lifetime
cancels waits; retired listener identities reject callbacks. Removal precedes cleanup,
View is detached before Destroy and proxy wrappers are released once by managed identity.
No View/proxy moves between Activities. These are SDK fixture observations, not a new
framework lifecycle or a retained-heap proof.

Recreation exposed an initial fixture bug: lazy Distinct called Java GetHashCode after an
earlier occurrence of the same proxy had been disposed. Native crash evidence was retained.
The fix materializes reference-identity uniqueness before disposal, and detaches the View
before destruction. Final validation requires two completed probes and old-fixture disposal;
process death fails immediately. Timeouts were not relaxed.

## Integration and qualification

The probe reuses WebViewRpcProtocol.JavaScript and AndroidWebViewPolicy escaping for a real
v1 data echo. #17 needs an optional provider channel carrying native evidence separately
from untrusted payloads, bound to canonical request/peer cancellation, with revocation before
navigation/Stop/JS-disable/dispose and unsupported-provider rejection. SDK callbacks do not
supply the framework's canonical document identity; observed test epochs are not authentication.

Fixture serialization uses source-generated JSON. Release properties are RunAOTCompilation=true,
PublishTrimmed=true, TrimMode=partial. This is Mono AOT/partial trimming, not NativeAOT/full
trimming or qualification of arbitrary application converters.

[Execution worlds](https://developer.android.com/reference/androidx/webkit/JavaScriptExecutionWorld)
are a separate research direction. The inspected newer bindings exposing them require API24;
provider feature availability and hostile-frame/DOM/lifecycle semantics still need tests.
They are not silently substituted into this API23 experiment. Alternatively, a strictly
immutable app realm excluding all untrusted code/frames needs an explicit verified trust
contract before #13/#17. No privileged implementation is approved by this ADR.

NOT EXECUTED: API23 runtime, physical device, genuinely unsupported provider, retained-Activity/
heap soak, background-process death/restoration, full trimming/NativeAOT and privileged bridge.
API34 success is not API23 runtime evidence. The meta redirect is not an HTTP302 claim.
Final execution counts, PR and hosted exact-head results are recorded with delivery.

## Execution report

- Product solution restore and full sequential Debug/Release builds: 0 warnings/errors;
  166/166 tests each, zero skipped. Windows WebView2 and ordinary Android sample native:
  RUNTIME PASS in both configurations, including resources, transport, RPC and lifecycle.
- Research APK Debug/Release builds: 0 warnings/errors. Both installed only on emulator-5554,
  API34 x86_64/provider113. Each `Validate.ps1 -Recreate` executed the full matrix twice and
  confirmed prior View/listener/proxy disposal. Both ended PRIVILEGED NO-GO as intended.
- Release APK contains `lib/x86_64/libaot-AndroidOriginProbe.dll.so`; actual properties are
  Mono AOT plus partial trimming. Final merged Debug/Release manifests both declare minSdk23.
- Resolved research dependencies include Core1.17.0.2, WebKit1.15.0.2 and Kotlin2.3.10.1;
  the ordinary sample's resolved dependency graph contains **zero** AndroidX packages.
- Selected NuGet archive SHA256:
  `D28F3D7C28E35B0FB77DA7C971DEA3834F47239E8E4B094B9B47E5BA45BA80BC`.
- Commands: pinned bootstrap/source root; solution restore, sequential Debug/Release build
  and `dotnet test --no-build --no-restore`; Windows `--validate`; ordinary Android signed
  APK/validation Intent; research `-t:SignAndroidPackage` in each configuration with JDK21
  and explicit-serial `Validate.ps1 -Recreate`. See the [probe README](../research/AndroidOriginProbe/README.md).

No release, package publication, main-framework/pin modification, authentication upgrade
or privileged dispatch. No template changes. The issue/PR remains for review; native SDK
research execution is separate from hosted compilation and the unexecuted lanes above.

Delivered in [PR #39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39),
implementation `7020ed7`, intentionally open for review. Hosted final-head status/SHA
is recorded in that PR separately from local SDK/native evidence.
