# Configuration inventory and security roadmap (#46, OPEN / PARTIAL)

This inventory distinguishes implemented public contracts from proposals. There is
no global security-disable switch. All capabilities remain experimental; current
Windows and Android QA limits are in [native qualification](native-platform-qualification.md).
No setting authenticates JavaScript or enables privileged C# handlers. A green build
is not a stable-platform or privacy-isolation claim.

Classes: **standard** is ordinary supported configuration; **advanced** needs a
review of scope/lifetime; **dangerous** deliberately accepts a named additional
risk. Proposed diagnostic IDs below are reservations, not implemented diagnostics.
Current product dependencies and Android API23 floor remain unchanged.

| Area / option | Status / default / class | Risk and real scope | Minimum / activation | Compiler + runtime warning | Required native evidence |
|---|---|---|---|---|---|
| `AllowedNavigationSchemes` | Implemented, HTTP+HTTPS+about:blank, advanced | Per-view main navigation filter; not a subresource/POST firewall | Windows pinned WebView2; Android API24+; runtime change revokes document work, no implicit reload | No security diagnostic; invalid/unsupported explicit failure | GET/redirect/POST and reentrancy; API23 unsupported |
| Scheme/host/origin allow/deny, mixed content, cleartext, headers/cache | Proposed, retain current restrictions, advanced/dangerous | Separate main-frame policy from all-resource interception; headers may omit redirects/subresources/POST; Android manifest/OS cleartext has app scope | Determine actual SDK/provider hooks before adding capability; manifest change explicit | Future IDs after API review; unsafe cleartext/mixed-content must warn | Actual TLS/HTTP, redirects, POST, frames, subresources and multiple views |
| `NewWindowPolicy` / explicit `OpenIn` | Implemented, Deny; UserInitiated standard; AllowScriptInitiated dangerous | Per opener; spam/phishing, opener DOM and shared profile; target owns lifetime; Android URI unknown | Windows initialized same environment/profile, Android API23 same Activity; immediate, no reload | **MFNWVSEC001** exact enum member + fixed `SecurityWarning` on changed dynamic value2 | Real parsed timer actors, deny, owned target, stale/reuse, close; native gesture metadata is not authentication |
| System browser / target-origin prompt / opener isolation | Proposed, no launch, advanced | External launch can disclose URL; `noopener`/profile isolation must be actual SDK guarantees; Android cannot preflight popup destination from this callback | Future separate API; no invented origin or async prompt in current callback | Future unsafe diagnostic where applicable | Owned URI dispatch, no-launch negatives, origin changes/opener attacks |
| Downloads | Existing deny (Windows cancel; Android no automatic handler); configurable download proposed | Saving untrusted bytes needs source/type/size/path constraints, progress/cancel/errors; content disposition/MIME not trust | WebView2 hooks vs Android DownloadListener provide different data; app owns destination/stream, OS permissions explicit | Future automatic-save unsafe API warning + runtime diagnostic | Real attachment, traversal, oversize, cancel, failure; current bounded no-file check is not all-download qualification |
| Camera/microphone/geolocation/notifications/clipboard/file chooser/media | Current default denial where adapter hooks exist; granular decisions proposed | Per-origin/document policy must expire; OS grants are separate; Windows file picker defaults need an explicit interception design; Android chooser currently denied | Each permission needs its SDK/OS/provider capability and native gesture rules; no implicit manifest permission | Future dangerous auto-grants warn on exact symbols + runtime decisions, redacted | Real requests with/without OS permission, frames, revocation and document retirement |
| `IsJavaScriptEnabled`, ExecuteScript, string transport/RPC | Implemented, JS false, advanced | Explicit application code execution/string input; baseline messages unauthenticated, no services | Windows/Android API23; runtime JS disable revokes document work; page feature checks for RPC | No new security warning; not advertised as isolation | Current native JS toggles, stale callbacks, RPC cancellation; API23 stock provider modern RPC remains unqualified |
| DevTools/debugging/console/context menus/zoom/gesture tuning | Proposed; no new debug opt-in | Debugging/console can expose secrets and profile state; Android remote debugging is process-wide | Detect actual APIs/provider; document Debug vs Release and global scope | Future remote-debug dangerous symbol + fixed runtime warning | Multiple views, process scope, release build and redacted output |
| Local-content catalog / `WebViewLocalContentPolicy` | Implemented immutable catalog, strict default; Application policy explicit advanced | Exact app-owned resources/CSP; no remote allow-all; local origin is not trusted caller identity | Actual installed local-content hooks; policy applies on next catalog navigation/reload | Current API has documentation, no compiler security ID | CSP directives, fetch/base, traversal, cross-catalog, redirect, native recreation |
| Unsafe inline/eval/data/custom CSP, file/content access/raw headers | Proposed, retain current denial, dangerous | XSS, same-origin/file disclosure, unrestricted network; prefer separate granular source lists over arbitrary headers | SDK-specific capability and immutable-per-document policy; no current API23/network parity promise | Future **MFNWVSEC002** reservation, not emitted today; exact unsafe API + redacted runtime warning | Actual XSS/inline/eval, nested frames, file/content attempts; policy must not silently weaken other catalogs |
| Cookies/storage/profile/session/private/clear-data | Windows provider user-data folder exists; richer public policies proposed | Windows environment/profile scope; Android CookieManager/provider and flags may be process/global; no per-view private mode claim | Profile creation vs immediate operations must match engine API; deletion needs ownership/lifetime | Future destructive/private-data operations need explicit contract, warnings where dangerous | Independent profiles/multi-view, process reuse, persisted cookies/storage, real cleanup |
| TLS certificate exceptions | Proposed only; current cancel/fail retained; dangerous | Never global bypass. If feasible: exact origins + error categories + developer session expiration; Android SSL callback lacks reliable main-frame metadata | WebView2 certificate hooks / Android SslErrorHandler differ; cannot promise scoped parity before experiments | Future **MFNWVSEC003** reservation; exact unsafe member + runtime diagnostic and local suppression | Real untrusted loopback certificate, wrong origin/error, redirects/subresources, expiry, new session and revocation |
| Diagnostics/events/source snapshots | Existing neutral errors/state/version; advanced | Source/query/profile paths can contain secrets. Current fixed popup warning contains none | UI-thread snapshots; callbacks follow native lifecycle; future opt-in telemetry per scope | MFNWVSEC001 fixed ID/text only; future console/verbose redaction needs own tests | Explicit redaction, bounded logging and cleanup; no network privacy proof from process module inventory |

## Delivery order and dependency gates

The first #46 increment is explicit popup handoff plus real consumer compiler and
runtime warnings. Download/permission policy is a **separate future PR**, after this
increment is reviewed. Further knobs are proposals, not hidden implementations.
No duplicate WebViewOptions/lifecycle/semantic tree is introduced.

`#46` remains OPEN/PARTIAL. `#15` remains PARTIAL; diagnostic execution-world research
does not unlock `#13`, `#17`, `#18` or interactive Blazor AttachPage. Existing origin,
relay and DOM confused-deputy counterexamples remain relevant. Android Java bridge
annotation is not origin authentication. A narrowly positive native diagnostic
counter experiment cannot authorize application services or generic RPC dispatch.

Preview (`#7`) and stable (`#8`) have separate gates: published framework packages,
clean package-only consumers, native host/device/provider coverage, manual keyboard/
IME/DPI/Designer, memory soak and security review. API23 stock-provider and current
focus/Designer failures keep broader readiness incomplete. No release/version/tag or
package publishing is part of this increment. Published 1.12 package alignment must
be a separate verified PR, without a silent source fallback.
