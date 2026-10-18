# Phase 2 trust matrix and architecture decision

Current follow-up: the [trust strategy ADR](hybrid-trust-strategy.md) defines a candidate
whole-application realm and alternative world research with explicit unmet gates.
Whole-realm authorization is distinct from identifying one script. Neither model is
qualified; all current product messages remain untrusted and privileges remain off.

Decision (2026-10-17): local static resources are usable independently of Hybrid/RPC.
There is no privileged message dispatch on either platform in this increment. Existing
ExecuteScriptAsync/PostMessageAsync/WebMessageReceived semantics remain compatible.
The application must treat their strings and informational Source as attacker-controlled.

| Situation | Windows baseline | Android baseline | Privileged Hybrid decision |
| --- | --- | --- | --- |
| Controlled top-level local document | Native WebView2 message callback; Source informational | AddJavascriptInterface has no sender origin | Unavailable until authenticated transport and document lifecycle gates pass |
| Remote top-level page | Ordinary untrusted strings | Ordinary untrusted strings | Deny |
| Child frame | Core WebMessageReceived is the top-level channel; no CoreWebView2Frame subscriptions | Annotated interface is reachable by child frames, Source=null | Deny; local CSP excludes frames; no promotion from top-level Source |
| Same-origin frame | No privileged registration | Indistinguishable caller through existing interface | Deny; same-origin is insufficient evidence of top-level frame |
| Opaque/data/srcdoc/sandboxed origin | No authorization inferred | No authorization inferred | Deny |
| Redirect to another origin | Baseline scheme/lifetime policy; local main-frame departure denied when reported | Incomplete redirect interception in SDK | Deny; resource origin is not a network sandbox |
| Forged request ID or claimed Source | IDs are spoofable lifetime correlation only | IDs are spoofable lifetime correlation only | Never authenticate from either value |
| Old document callback after navigation/Stop | Shared request/lease guard | Shared guard plus browser-client generation | Drop obsolete callback; a current ID still confers no privileges |
| Retired peer / Activity replacement / dispose | IsCurrent + unsubscribe/controller close | Fresh View/client, revoke generation/snapshot before cleanup | Drop; no pending authorization carried across documents |
| Out-of-order asynchronous reply | Bounded #12 RPC correlation over untrusted strings | Bounded #12 RPC correlation over untrusted strings | Data only; no authorization or C# dispatch |

Local CSP, exact origin/path matching, MIME and no-CORS responses limit content delivery.
They are not evidence that a native string callback came from an authorized frame. A
future optional Hybrid layer must consume authenticated transport metadata separately
from the baseline event and fail closed on unsupported engines. It must not expose CLR
objects, services, filesystem paths, reflection or arbitrary methods. Handler registration
must be explicit, scoped, allowlisted and compatible with source-generated JSON/trimming/AOT.
No ASP.NET Core dependency belongs in ordinary WebView.

The baseline now exposes `RequestId` and `PeerGeneration` on received messages for
forwarding/lifetime correlation, and `IsOriginAuthenticated` is **always false**.
`AuthenticatedMessaging` is a reserved capability that neither adapter advertises.
A same-origin iframe can invoke `parent.modernFormsWebView.postMessage`; Windows then
reports the parent channel/Source. A top-level callback alone does not identify the
original script caller. No allowlist of informational Source values upgrades this event.

## Native API evidence and pending research

WebView2 documents [CoreWebView2.WebMessageReceived](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.webmessagereceived)
for the top-level document, with separate frame APIs. Document swaps and delayed native
dispatch still need a privileged-transport protocol; this increment does not upgrade
the existing callback/Source into authentication.

Android's [AddJavascriptInterface warning](https://developer.android.com/reference/android/webkit/WebView#addJavascriptInterface(java.lang.Object,%20java.lang.String))
explains frame exposure and lack of reliable caller origin. The product dependency set
contains no AndroidX WebKit binding. API23 platform WebMessagePort alone does not establish
the required incoming frame identity. AndroidX
[WebMessageListener](https://developer.android.com/reference/androidx/webkit/WebViewCompat.WebMessageListener)
provides native sourceOrigin/isMainFrame, but actual availability depends on the installed
WebView provider's WEB_MESSAGE_LISTENER feature, not only Android API level. #16 must
prototype exact-origin rules, reject opaque origins/subframes, check supported/unsupported
providers and stale reply proxies, and justify a pinned binding dependency before #17.
The subsequent [#16 research prototype](issue16-android-origin-research.md) uses a separate
SDK APK and a pinned research-only dependency. It verifies direct-frame metadata and a
same-origin parent-object relay counterexample: privileged original-caller policy NO-GO.
The [#15 Windows hardening](issue15-windows-message-hardening.md) rejects stale packet
ID substitution without upgrading this authentication policy. #15/#17 remain open gates.

Regression evidence additionally exercises same-origin parent-helper invocation, direct
opaque iframe bridge calls, stale packets after native document replacement, Windows
foreign HTTP iframe/HTTP302 and Android page-initiated meta redirect. Every accepted
baseline event remains untrusted. Privileged transport qualification remains NOT RUN
because that transport is not implemented; these tests do not qualify #15/#17.
The [#12 protocol](issue12-rpc.md) adds malformed/oversized/version/duplicate/deadline
checks separately from the static-resource tests. It supports only explicit app-to-page
JavaScript calls and untrusted data responses; no C# handler exposure or authenticated
platform capability is added. See [phase2-progress](phase2-progress.md) for execution results.
