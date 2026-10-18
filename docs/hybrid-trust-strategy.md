# Hybrid trust strategy ADR — #10/#15/#17 (2026-10-18)

Delivery update: #43 was normally merged after [fresh final review](pr41-43-final-audit.md).
The candidate models remain unqualified. The [new catalog policy](local-content-policy.md)
controls content loading only; it is not the verified realm/native channel proposed here.

## Decision and scope

**Privileged JavaScript → C# remains NO-GO on both current product adapters.** This ADR
selects a concrete candidate for further research: a whole, immutable trusted application
realm with an independently qualified native channel. It does not authenticate a specific
script, implement a handler, relax baseline CSP or promote authentication capabilities.
The candidate's acceptance gates below are currently unmet. #15 remains OPEN/PARTIAL;
#17/#13/#18 and privileged #19/#20/#21 remain blocked. Full Blazor #23–#31 requires a
separate positive #22 decision after its content/security obstacles are resolved.

Inputs are the executed [AndroidX #16 counterexample](issue16-android-origin-research.md),
[Windows #15 hardening](issue15-windows-message-hardening.md), [#33 native matrix](issue33-security-validation.md),
and [#22 executable Blazor NO-GO](issue22-blazor-feasibility.md). No second NativeViewHost,
layout, focus, dispatcher, request counter or cancellation system is proposed.

## Identity, trust and threat model

| Evidence | Meaning | Does not establish |
| --- | --- | --- |
| RequestId/PeerGeneration/document token/nonce | Current work/peer correlation and rejection of stale packets | Authority, original caller or user intent |
| Native Source/origin/main-frame metadata | Engine-reported receiving object/frame context under that API's contract | Which same-origin script invoked or relayed through that object |
| Immutable asset identity verified by the native application | The allowlisted bytes selected by an already trusted app deployment | Safety of every use of untrusted data or all later execution |
| Whole trusted application realm, if qualified | All code able to reach the privileged channel is authorized app code | Identity of one script among those equally trusted scripts |
| Isolated execution world, if qualified | A separate JavaScript execution context and its native entrypoint | Integrity/authorization of shared DOM data, relayed events or a particular user action |

Attacker inputs include remote documents, foreign/opaque/same-origin frames, markup/data
rendered by trusted code, forged packets, stale callbacks, redirects, navigation races,
tampered or unlisted resources, and a provider lacking required features. The app package,
OS/engine implementation and explicitly approved application code are trust roots;
compromise of these roots is outside this model. Compromised third-party app scripts or
an XSS inside the trusted realm invalidate its premise and can exercise its privileges.
The design must minimize the consequences with explicit application authorization.

#16 positively observes a child invoking its parent's listener object: native origin and
isMainFrame describe the parent context. Windows positively observes the same attack on
the parent helper and receives the parent's Source. #15's new native token rejects old
packet ID substitution but cannot reject a call through the current valid parent helper.
This is why strengthening a nonce, comparing Source or checking a top-level event cannot
repair the original-caller model. If hostile code must execute in the same realm with
the same reachable objects, a privileged main-world channel remains NO-GO.

## Candidate A: whole trusted application realm

1. The native app explicitly selects an immutable deployment manifest of reviewed
   HTML/JS/CSS/modules/resources, identities and bounds from its own trust root. Hashes
   supplied by the page are not a trust root. Bundled script updates and dependencies
   require the same review as managed application code. Catalog bytes are public, never
   a secret store. A local HTTPS host alone grants no authority.
2. An optional trusted-content contract must enforce that only those executable bytes
   can run. Exclude external scripts, eval/string execution, injected executable HTML,
   untrusted same-origin files, arbitrary base changes, frames (including srcdoc/data/
   sandbox/about:blank), workers/service workers, popups/opener relationships and app
   proxies that relay messages from untrusted content. Render untrusted values as data;
   DOM/script injection into the trusted realm is a security boundary violation.
3. Display remote/user HTML in another native view with **no privileged registration**,
   no shared DOM/opener relationship or authority-forwarding postMessage relay. A baseline
   string event, if present, remains untrusted. This proposes no new public messaging-off
   property. Android baseline cookie/profile state can be process-shared; separate views
   do not imply storage/process isolation or protect credentials by themselves.
4. A platform-qualified channel must bind native evidence to the exact controlled content,
   current canonical request/peer and the declared realm trust policy. It must not promote
   existing baseline events. Install it before controlled load only where lifecycle and
   feature evidence suffice. Unsupported/ambiguous engine, navigation or frame states
   leave privileges off; there is no privileged legacy fallback.
5. Any document replacement, reload, redirect/departure attempt, Stop, JavaScript disable,
   initialization failure, peer/Activity replacement or Dispose revokes authority and
   cancels pending handlers before native teardown or new document exposure. A native
   completion/queued reply cannot re-arm authority. Recovery requires a fresh verified
   attachment, using existing request tokens/site.IsCurrent and dispatcher checks.
6. Future #13 registers only explicit typed operations, argument/result schemas and
   least required application permissions, with source-generated JSON metadata, bounds,
   timeout/cancellation and safe error mapping. No CLR objects/reflection/service locator,
   arbitrary paths or dispatcher handle are exported. Authorize each operation in native
   application policy; channel trust does not prove a user intended a sensitive action.
   Scopes/handlers belong to the canonical document token, not a static registry.

This is an architectural proposal, not a statement that today's immutable catalog enforces
all six requirements. NavigationStarting is not a complete network/frame sandbox. CSP and
resource interception require native negative tests for every supported engine and fail
closed where enforcement/lifecycle cannot be established. Android's current SDK callbacks
are insufficient for that promotion. Merely trusting bundled content must not enable the
existing AddJavascriptInterface as a privileged path.

The proposed optional API responsibilities, **not shipped type names or callable APIs**:

| Responsibility | Required contract |
| --- | --- |
| Application content declaration | Immutable reviewed asset set, entrypoint, explicit whole-realm trust opt-in; no arbitrary trusted URI string |
| Qualified platform attachment | Native-only evidence and opaque revocable authorization lease, specific to one canonical context; unavailable by default |
| Explicit handler registration | Typed allowlist scoped to that lease/document with bounded source-generated serialization and application authorization |
| Invocation | Verify lease + canonical context before dispatch and again before reply on existing MFN UI queue; invoke existing cancellation/error mechanisms |
| Disposal/recovery | Cancel/revoke first, detach subscriptions and dispose scopes/resources, then recreate using existing host contract |

The lease is a proposed internal authorization result of satisfied invariants, not a token
sent by JavaScript or a second document lifecycle. Existing IsOriginAuthenticated stays
false. A future whole-realm capability must name its actual guarantee and must not claim
to identify individual scripts by reusing that flag. Any public API change needs review.

## Platform mechanisms and candidate B: isolated worlds

Windows SDK1.0.4258.31 exposes ordinary CoreWebView2 WebMessageReceived, separate frame
APIs, document-created script injection and CallDevToolsProtocolMethodAsync. Its pinned
Core XML contains no ExecutionWorld/IsolatedWorld-named public member. This is an inspected
API inventory, not proof that Chromium lacks isolated worlds.
[Microsoft security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)
requires validating content/messages and restricting exposure. Its Source checks do not
contradict the measured same-origin parent-object counterexample.

CDP [Page.createIsolatedWorld](https://raw.githubusercontent.com/ChromeDevTools/devtools-protocol/master/pdl/domains/Page.pdl)
is a real protocol command returning an execution context for a frame. It is not marked
experimental in the inspected current schema; it is also not a qualified privileged
WebView2 bridge. The SDK's generic CDP call is public; Microsoft notes
[CDP is maintained by Chromium](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/chromium-devtools-protocol).
A future experiment must bind context/frame/loader lifetimes, native binding events and
replies to the canonical peer, verify runtime versions, teardown/navigation ordering and
unreachable entrypoints from page/child worlds. Do not request universal access, bypass
CSP, enable remote debugging or expose CDP to page code as a shortcut. No CDP/world
execution or native authentication qualification was performed here.

Android [AddJavascriptInterface](https://developer.android.com/reference/android/webkit/WebView#addJavascriptInterface(java.lang.Object,%20java.lang.String))
exposes the annotated object to frames and cannot identify the original script. It stays
unprivileged. The researched AndroidX
[WebMessageListener](https://developer.android.com/reference/androidx/webkit/WebViewCompat.WebMessageListener)
reports native origin/main-frame metadata, but the executed relay disproves the stronger
identity claim. Research1.15.0.2 retains minSdk23 and tests WEB_MESSAGE_LISTENER; no
AndroidX dependency is introduced into product adapters.

[JavaScriptExecutionWorld](https://developer.android.com/reference/androidx/webkit/JavaScriptExecutionWorld)
and [WebViewCompat world injection/listener APIs](https://developer.android.com/reference/androidx/webkit/WebViewCompat)
are real APIs added in WebKit1.16.0. getExecutionWorld requires provider feature
JS_INJECTION_IN_FRAME_AND_WORLD; injected scripts can call listeners registered in the
same world. Inspected Microsoft bindings1.16.0.1/1.17.0 contain AAR minSdk24 (rechecked
against1.15.0.2's23). These are not available by assumption on API23/provider113, and no
minimum/dependency update is authorized here. A newer optional experiment needs explicit
dependency/API-floor review, supported and genuinely unsupported provider runs, exact
origins and a full hostile-frame/navigation/lifetime matrix. No fallback to the old bridge.

For **either** platform, an isolated world experiment must reject confused-deputy requests
through shared DOM mutations, synthetic events and postMessage relays. A trusted isolated
script forwarding attacker-controlled DOM commands can still invoke privileged operations.
This is a threat-model inference, not a claimed executed exploit in an isolated world.
There must be no page-world callable proxy into that world. If actual isolation or safe
input/operation authorization cannot be demonstrated, candidate B also stays NO-GO.

## Blazor content boundary

The unmodified .NET10.0.12 startup fetches its initializer manifest. Today's fixed
connect-src 'none' blocks it and base-uri 'none' rejects its base element, measured by #22.
A possible future **separate trusted-content contract** may allow only required immutable
same-origin asset fetches and one declared base URI, with exact route fallback/method/MIME/
size rules. It must retain the no-untrusted-code/frames/workers rule and exclude remote
fetch/services. This requires review and real enforcement tests, not a CSP override here.
The stock WebViewManager Source/base check is not the authorization layer. Its inbound
dispatch exposes managed interop/page services, so it remains disconnected from baseline
events. Passive Razor/DI/dispatcher PASS cannot authorize interactive Blazor or full #23–#31.

## Acceptance evidence required before enabling any privilege

| Gate | Required positive and negative evidence | Current status |
| --- | --- | --- |
| Trusted asset execution | Reviewed manifest; tamper/unlisted/mutable/external/eval/HTML injection denied, including modules and path/MIME variants | Existing catalog/CSP bounded tests only; new trust contract NOT IMPLEMENTED |
| Frame and realm isolation | Authorized app call succeeds; all direct/parent/helper/opaque/foreign/same-origin/opener/DOM-relay attacks cannot invoke a handler or receive its reply | Parent relay counterexample EXECUTED on both; qualified realm/world NOT EXECUTED |
| Platform channel | Exact native metadata separate from payload; provider/version matrix, unsupported provider fails closed with zero handler calls | AndroidX research only; no qualified product channel |
| Redirect/network policy | Actual HTTP302/307, POST, meta/script/history/reload/frame/worker cases cannot retain authority or disclose data | Bounded baseline tests; complete privileged enforcement NOT IMPLEMENTED |
| Lifetime/races | Revoke before replacement/Stop/JS setting/failed init; delayed dispatch/reply, reentrant navigation, controller/Activity recreation, same-URI reload cannot reauthorize | Canonical baseline guards exercised; privileged scopes NOT IMPLEMENTED |
| Handler authorization | Explicit operation/argument/result bounds, malformed/version/duplicate floods, native policy denial, cancellation/errors/disposal; zero service discovery/secrets on rejection | #12/#19 unprivileged RPC only; #13 BLOCKED |
| Deployment and tooling | Actual API23 and declared floor runtimes, physical devices, engine/provider matrix, AOT/trim compatibility where claimed, retained-scope/heap tests | API34 emulator and current Windows only; broader lanes NOT EXECUTED |

Every rejection test must positively execute the attacker path and include a successful
authorized control/fence where a privileged path exists. An absent callback alone, forced
feature flag, constructor success or passively rendered HTML is not evidence of isolation.
No timeout/skip/threshold/warning relaxation may turn a blocked gate green.

Next authorized research unit should select one candidate and state its narrower guarantee,
with executable native probes **before** product handler work. A whole-realm contract can
be evaluated separately from the original-caller requirement; changing that requirement
requires explicit issue acceptance, not closing #15 based on this ADR. Baseline #7/#8
have independent qualification/packaging/approval gates and do not depend on Hybrid.

## Delivery and validation

The separate [execution-world probe](windows-world-experiment.md) now exercises the
public SDK in an unprivileged hostile fixture. A measured DOM confused-deputy attack
preserves the privileged NO-GO; it does not qualify this ADR's whole-app realm.

Separate strategy branch/PR; no runtime/public API/dependency/policy changes in this stage.
Delivered in open [PR #43](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/43),
implementation `2443c61`, based on verified #42 for sequential review; no automatic merge.
Fresh Debug/Release product, AndroidX recreation and Windows Razor probe results are
recorded in the [maturity report](phase2-maturity-report.md). They exercise current
fail-closed behavior and measured counterexamples, not authentication of this proposed
realm/world. Exact commits/hosted jobs are recorded with PR delivery. No new PR auto-merge,
issue closure, main-framework/pin modification, NuGet or release publication.
