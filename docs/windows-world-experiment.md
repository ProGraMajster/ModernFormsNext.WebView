# Execution-world diagnostic ADR (#15/#16/#10)

## Decision

**GO for a bounded public-SDK Windows research probe; NO-GO for privileged Hybrid.**
World identity does not establish the original caller or authorize a DOM-derived command.
This experiment does not implement or claim the proposed whole trusted application realm.
The fixture deliberately contains hostile scripts and therefore does not meet that
realm's premise. #15 remains PARTIAL/OPEN; #17/#13/#18 remain blocked. Android's existing
AddJavascriptInterface remains unprivileged and unauthenticated.

## Mechanism and boundaries

The public WebView2 SDK can call CDP Page.createIsolatedWorld and Runtime.addBinding.
The probe uses a unique native world name and a binding restricted by executionContextName,
then requires the exact native event executionContextId and the existing
WebViewTransport.CurrentDocument token/generation/readiness. The deprecated addBinding
executionContextId selector is not used. IDs in message payloads are never native evidence.
No persistent automatic script/binding is installed for future navigations.

The production Windows provider keeps its Core private. Instead of reflection or a new
product debug API, a small internal SDK fixture implements the existing IWebViewPeer seam.
It borrows the canonical WindowsNativeViewSite container and hosting/focus/dispatcher.
NativeViewHost owns its peer/controller. It reuses the exact product navigation
acknowledgement queue and canonical client IDs, with no second document counter.
It is **not a qualified product backend**: local-content delivery, user-agent settings
and general backend parity belong to the real adapters and separate regression tests.

Only a bounded counter and bounded fixture payload set are attached. There is no
privileged dispatch, DI, file access, service or reflection. Default/app CSP delivery,
permissions, TLS and authentication policy are unchanged.

## Native matrix

The initial successful Debug probe used WebView2 SDK1.0.4258.31/runtime155.0.4283.62
and .NET SDK10.0.401. Final Debug/Release evidence and exact-head hosted build checks
are recorded on the separate experiment PR. CI compilation is not native-world execution.

| Case | Required evidence / interpretation |
| --- | --- |
| Feature detection | Native world creation, named binding and real positive callback; unsupported method explicitly rejected without fallback |
| Main page | HTML-parsed actor runs; binding and isolated global are undefined |
| Same-origin iframe/parent | Real iframe runs; own and parent binding undefined; relay executes and remains untrusted |
| Opaque sandbox | Real script runs; binding undefined and parent DOM access denied; postMessage relay still executes |
| Forged CDP-looking JSON | Baseline page message remains untrusted; cannot manufacture a native bindingCalled event |
| DOM confused deputy | Page and same-origin iframe modify shared DOM and emit an event; genuine isolated script forwards both commands into its native counter |
| Stop | Canonical token revoked; recorded packet and a **live** native binding callback both rejected |
| JavaScript disable/re-enable | Old canonical token remains revoked; recorded packet cannot restore it |
| HTTP302/foreign origin | Real 127.0.0.1 to localhost redirect retires document; binding absent, old binding evaluation fails, old packet rejected |
| Same-URI reload | Old canonical token/packet rejected; fresh native world positive control succeeds |
| Cross-window host recreation | Old peer/site/token rejected; higher canonical generation and new world positive control |
| Dispose | Old captured packet rejected; receiver unsubscribed and controller owned by normal host lifetime |

The DOM counterexample is a required successful attack against the **naive counter**,
not an enabled C# handler or a proof against a correctly constrained whole-app realm.
The loaded page/iframe scripts actually execute. Native evaluation invokes those actors
but is not used to pretend a CSP-blocked actor loaded.

Recorded-packet tests exercise the guard explicitly; they are not described as actual
queued SDK callbacks after retirement. Stop additionally executes a real native callback.
Context IDs may be reused by engines; the canonical token remains mandatory.

## CSP and platform qualification

Fixtures use raw, explicitly unprivileged HTML **without CSP**. This avoids weakening
or pretending to qualify the production local-content policy. Current CDP documents a
world CSP parameter whose omission can clear world CSP; inheritance/effects on the pinned
SDK/runtime and future versions are **NOT QUALIFIED**. Universal access is false.
No setBypassCSP, remote debugging port, command-line security bypass or all-context binding.

Public SDK method availability does not constitute original-script authentication.
The exact smaller guarantee observed is separation of world globals/binding visibility
and native context metadata in this fixture. Future use must specify operation
authorization independently and prove that hostile DOM/messages cannot select commands.

Android execution worlds, product world integration, API23/physical device, foreground
keyboard/UIA/manual visual, Designer/full IME/DPI/accessibility/heap and trimming/AOT
remain NOT EXECUTED. Baseline Android regression is a separate evidence lane.

Initial red diagnostics are retained under ignored artifacts/world-*: unsupported CDP
methods project E_INVALIDARG as ArgumentException; raw HTML Reload can yield an empty
SDK Source and needs the existing nullable URI convention; removing/readding within the
same Form is not proof of a new host. The fixture now tests a real cross-window transfer
and requires the old peer to retire plus a higher canonical generation. No security
criteria, timeout, warning or threshold was relaxed.

Primary contracts: [CDP Page](https://chromedevtools.github.io/devtools-protocol/tot/Page/),
[CDP Runtime](https://chromedevtools.github.io/devtools-protocol/tot/Runtime/), and the
[WebView2 SDK public API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/icorewebview2).
Future SDK/protocol versions need new native qualification; this experiment exposes no
new product API or capability.
