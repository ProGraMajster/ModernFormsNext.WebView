# #33 — baseline messaging security verification

Delivery: [PR #36](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/36),
implementation `c001685`. The PR records terminal checks on its exact final head.

Branch `feature/issue-33-message-security`, based on merged main `79eaff5`.
This verifies the untrusted baseline and makes its missing authentication detectable;
it does not implement privileged Windows/Android messaging or a handler registry.

Received events now carry immutable original RequestId/PeerGeneration metadata and
IsOriginAuthenticated=false. AuthenticatedMessaging is a reserved, absent capability.
Existing constructors, payloads, Source and script/message submission semantics remain.
Pure tests cover foreign/opaque/claimed Source, current spoofable IDs, navigation, Stop,
cancel, JS disable, dispose and observer reentrancy without relabeling an old event.

| Native case | Windows WebView2 155 | Android API34/provider113 |
| --- | --- | --- |
| Same-origin iframe calls parent helper | Received, untrusted; parent channel does not identify original caller | Received, untrusted, no sender origin |
| Sandboxed srcdoc opaque iframe calls native bridge | Frame channel not subscribed; fixture execution positively confirmed | Received through AddJavascriptInterface, Source=null, untrusted |
| Old document packet after replacement + current fence | Old rejected, current retained and untrusted | Old rejected, current retained and untrusted |
| Real foreign HTTP iframe | Frame script executes, no subscribed frame callback | NOT RUN: no cleartext/TLS relaxation introduced |
| Cross-origin HTTP302 | PASS, owned random-port loopback fixture | NOT RUN: separate native hook/provider qualification |
| Page-initiated meta redirect | Not the HTTP302 substitute | PASS via existing HTTPS fixture; new logical identity, no authentication upgrade |

Debug and Release restore/build/tests: PASS, zero warnings/errors; 121/121 each
(94 shared, 15 Windows, 12 Android policy), zero skipped. Both Windows native and
Android signed APK/emulator validators: RUNTIME PASS in both configurations, including
the full pre-existing resource/navigation/script/lifecycle suite. The initial HTTP302
timeout exposed serialized test-server idle connections; bounded concurrency, per-request
timeout and disposal replaced that fixture limitation, without changing browser policy.

No new dependencies, pin/framework modifications, C# handler exposure, automatic origin
approval or release. Physical/API23/foreground keyboard/Visual Studio Designer/full
IME/DPI/accessibility/retained-heap/long-soak and privileged bridge qualification are
NOT RUN. Existing AddJavascriptInterface remains incapable of origin authentication.

CI and PR delivery are recorded in this stage's PR and phase2-progress. #11 may consume
the correlation metadata only after this stage's exact-head checks pass; it must still
reject stale/reentrant events and preserve this fail-closed capability.
