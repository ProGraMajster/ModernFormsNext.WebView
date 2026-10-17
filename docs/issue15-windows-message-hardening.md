# #15 — Windows message hardening and security decision

## Scope and decision

**PARTIAL implementation; privileged original-caller policy NO-GO.** This increment
hardens the existing Windows adapter's untrusted string path. It does not complete the
issue's requirement to authenticate an original caller in the presence of hostile
same-origin frames. Issue #15 must stay open. There is no handler dispatch, trusted
message flag, host object or new public control/transport API.

Resources, security verification, reusable transport and RPC are merged through #35–#38.
This branch follows the verified Android research delivery in
[PR #39](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/39). The Windows
change is a separate dependent PR and must remain open for maintainer review.

## Architecture and implemented behavior

- `WindowsMessageGuard` is an internal UI-thread helper inside the existing peer, using
  the canonical logical request ID. It has no second document counter, host or dispatcher.
- The private Windows packet is now `[requestId, opaqueToken, string]`. The supported
  page API remains `window.modernFormsWebView.postMessage(value)`. The token is generated
  natively per request, captured in that helper, and never added to public diagnostics.
  A document can observe its own packet; this is correlation, **not script authentication**.
- Substituting a new public ID into an old captured packet fails. New peer instances
  receive independent tokens. Malformed, guessed and legacy private packets are rejected.
- For controlled local resources, native message Source must match the current collection's
  parsed HTTPS host and effective port. Credentials, foreign/prefix-spoofed hosts, HTTP
  and opaque/missing sources fail. Payload claims do not influence this check. Raw HTML
  and ordinary remote-page Source remain informational; their baseline API is unchanged.
  Controlled resources retain their existing navigation restriction and CSP.
- Navigation commands prepare the guard before native submission. The first native
  Starting acknowledgement is idempotent for the same request. Reload/replacement
  rotates the token; canceled/failed navigation, Stop, JS disable and controller cleanup
  revoke it. Revocation for an old request cannot revoke a newer reentrant request.
- Successful completion may retrieve only an existing current token. It cannot create
  or restore one after Stop or a document change. Already submitted native script can
  have side effects, but its old packet cannot acquire current correlation.
  JS disable/re-enable during Starting still settles navigation without restoring a
  revoked helper; reload recovers it. Helper-injection failure also revokes correlation.
- Existing peer/site/client guards, native NavigationId map, command queue, cancellation
  and controller ownership remain authoritative. No subscription to child-frame messages
  is added. Observer exceptions remain outside parser catches.
- Ordinary strings retain their empty/large-string behavior; RPC independently applies
  its bounded v1 envelope. The Android product adapter and shared APIs are unchanged.

## Why privileged messaging remains unavailable

Microsoft documents the Core
[WebMessageReceived event](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.webmessagereceived)
as the top-level channel. That identifies the receiving JavaScript object/context, not
every script that can invoke it. The native #33 fixture positively executes a same-origin
child calling `parent.modernFormsWebView.postMessage`. Its event has the parent's Source.
The new token does not change that result: the child invokes the current valid helper.

Therefore neither a top-level event, matching Source, request ID nor token is authority
to execute C#. See [WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security).
`AuthenticatedMessaging` remains absent and `IsOriginAuthenticated` remains false.
No response may contain application secrets based on these baseline messages.

A privileged design needs an explicit verified trust contract: for example an immutable
application realm excluding untrusted code/frames, or a separately researched execution
world. An allowlist alone is insufficient. No such mechanism is silently introduced
here, and no empty Hybrid API is supplied to imply completion.

## Tests and native evidence

22 new deterministic cases cover idempotent preparation, replacement/revocation,
old completion lookup, reentrant cancellation, cross-peer ID substitution, parsed origin
spoofs, malformed/types/guessed/private legacy packets and baseline string compatibility.
They compile the exact internal guard source using the existing adapter-test pattern.

The real Windows validator captures its own original private packet without logging its
token, reloads the same local collection, substitutes the current public ID and submits
the forgery. It also submits a guessed token and legacy packet, then waits for a positively
received current-helper fence. Only the valid baseline message arrives. Stop, JS setting
changes and reload recover the baseline with authentication still unavailable.
An additional real Starting observer disables/re-enables JS and requires navigation to
complete, catching the revoked-helper completion race without increasing its timeout.

The full common native suite additionally exercises same-origin parent-helper invocation,
opaque and foreign frames, actual cross-origin HTTP302, document replacement, RPC results/
timeouts/cancellation, resource CSP/MIME, reentrancy, initialization retirement, 50 fresh
leases and cross-window recreation. These are automated native executions, not human UIA
or visual qualification. Android runs the unchanged ordinary adapter regression suite.

Commands and completed results are recorded in the delivery section and
[Phase 2 report](phase2-followup-report.md). Hosted CI compilation is a separate lane
from locally installed WebView2/emulator execution.

## Limits and next work

Windows runtime: WebView2 155.0.4283.60; SDK 1.0.4258.31. Android regression environment:
API34 x86_64, provider com.google.android.webview/113.0.5672.136. There is no new runtime
minimum or dependency for this Windows change.

NOT EXECUTED: other WebView2 versions/OS floors, foreground keyboard injection,
interactive Visual Studio Designer, full IME/DPI/accessibility/retained-heap soak,
Android API23 runtime, physical device and privileged bridge qualification. No leak
freedom, isolation from arbitrary same-origin scripts or complete network sandbox is
claimed. Native cancellation cannot undo JavaScript already submitted to the engine.

An initial build found a missing Xunit import in the new test file. Final source review
also found that returning early for a revoked helper could strand navigation after a JS
toggle during Starting. Completion now settles without restoring the token, with a real
native regression. Both fixes precede final validation. No warning suppression, retry/timeout relaxation or security
policy weakening was used. Main framework, framework pin, templates, versions and
publication remain untouched.

#13/#17/#18 stay gated by the unsatisfied privileged contract. #19 can independently
extend existing unprivileged RPC error/cancellation behavior without exporting handlers.
#22 is a separate feasibility experiment, not a full BlazorWebView implementation.
