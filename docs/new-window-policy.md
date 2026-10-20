# Explicit new-window handoff (experimental, #46)

`NewWindowPolicy` defaults to `Deny`, preserving ordinary WebView behavior. A ready
adapter reports `WebViewCapabilities.NewWindows` only when its native hooks exist.
`UserInitiated` offers only requests carrying the engine's gesture indication.
`AllowScriptInitiated` also offers timers/script requests, with compiler warning
`MFNWVSEC001` and the matching runtime `SecurityWarning` event. Neither choice opens
anything until the application synchronously selects a target.

```csharp
// Run on the owning UI thread. Both controls must already be hosted and initialized.
browser.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
browser.NewWindowRequested += (_, e) => e.OpenIn(freshTarget);
freshTarget.CloseRequested += (_, _) => freshTarget.Dispose();
```

The target must be separate, ready and never navigated, scripted or messaged. It is
consumed once. Windows requires the same provider environment/profile; Android
requires the same Activity context. Create another target for another request.
There is no asynchronous deferral, automatic Form creation or system-browser launch.
Keeping event arguments and accepting later throws. A source Stop/navigation,
JavaScript disable, disposal or policy change during the callback invalidates the
decision. A target navigation/retirement cannot be overwritten by the old decision.
All identities and cancellation come from the existing WebView/NativeViewHost lease.

## Deliberate higher-risk configuration

```csharp
browser.SecurityWarning += (_, e) => LogFixedDiagnostic(e.DiagnosticId, e.Message);
// Reviewed exception: accepting timers enables popup spam, phishing and opener exposure.
#pragma warning disable MFNWVSEC001
browser.NewWindowPolicy = WebViewNewWindowPolicy.AllowScriptInitiated;
#pragma warning restore MFNWVSEC001
```

The warning is attached to that exact enum member using `Obsolete(..., false,
DiagnosticId = "MFNWVSEC001")`. It signals security risk, not planned removal.
This uses the C# compiler and needs no analyzer dependency or analyzer packaging.
With warnings treated as errors, this explicit local suppression (or the application's
reviewed diagnostic policy) is required. Safe values have no security warning.
`Enum.Parse`/JSON cannot be statically classified; invalid numeric values throw before
mutation, and a changed value of 2 emits the runtime warning. Equal assignments do
not repeat it. Subscribe before applying configuration. Runtime messages contain only
fixed text/ID, never page URLs, scripts or profile paths. Applications must consume
this event if they require a visible runtime log or prompt.

## Native boundaries

Windows uses the pinned WebView2 `NewWindowRequested.NewWindow` API and a fresh,
already initialized controller. Document-created bridge registration is completed
before assignment; resource interception is registered after the SDK replaces the
target's web contents, as required by the SDK. The target keeps its own permission,
scheme and local-resource policies. The source catalog is **not** inherited. A
reserved local resource address without a target catalog remains denied.

Android uses `WebChromeClient.OnCreateWindow` and `WebViewTransport`; min OS remains
API23. Its callback has no destination URI, source origin, frame identity or native
navigation ID. `RequestedUri` is null. Never reconstruct identity using hit tests or
the current top-level URL. The current request correlates lifetime only; it does not
prove which document/frame originally queued the intent. Initial `about:blank` plus
opener DOM writes can omit `OnPageFinished`; a guarded API23 visual-state callback
confirms the initial render state. The target's Java interface is absent from this
already-created blank document until a subsequent load. An API23 WebMessagePort
therefore bootstraps the same untrusted string envelope/helper for this document;
completion waits for that bootstrap. Both native finish/visual callback orders use
the same path. Navigation, Stop and disposal close the native endpoint. The page owns
the transferred endpoint; cleanup closes only the native-owned endpoint. This adds
no public transport or lifecycle counter. Wildcard port transfer to an opaque blank
page is explicitly **not** origin or caller authentication; an untrusted page can
relay the port/helper. Real URL navigations retain normal SDK callbacks and Java
bridge. Bootstrap failure reports Engine for the original request without a global
or privileged fallback. This is render/lifetime readiness, not a network policy.
Long popup/port JNI retention soak is not established by bounded handoff tests.
The port uses the target's original canonical document cancellation token. A disabled
JavaScript setting closes it; re-enabling does not restore an old endpoint. Navigate
or reload to establish a fresh messaging document. Cancellation during its initial
bootstrap fails that original request with Canceled; a later request is preserved.

The SDK gesture indication is an intent filter, not an authorization boundary.
Application script execution can confer transient activation on Windows. Timers in
the native fixture therefore wait beyond that activation, without script polling.
SDK deny behavior can return a testing WindowProxy, so `window.open() === null`
alone is not a portable proof that no target opened.

Opener access, same-origin DOM access, cookies and engine/profile state can remain
shared. The application explicitly accepts these risks; no privacy isolation or
`noopener` guarantee is added. `window.close` raises an informational event only;
the application still owns its controls/Forms. It may originate from an opener,
including when the target's own JavaScript is disabled.

`IsOriginAuthenticated` remains false and `AuthenticatedMessaging` unavailable.
There are no C# service handlers, permission grants, TLS exceptions or hidden changes
to CSP/downloads. An unsupported ready adapter throws `NotSupportedException` for an
opt-in. A queued unsupported configuration fails initialization before navigation
replay; recreate a control with supported settings to recover.

## Validation

`scripts/test-security-consumer.ps1` creates external, isolated C# consumer projects:
safe 0 diagnostics; unsafe exact MFNWVSEC001 warning/risk/safer alternative; locally
suppressed 0 diagnostics under warnings-as-errors; unsuppressed warnings-as-errors
fails with that exact ID. All four cases run in Debug and Release for each snapshot.
Shared tests exercise dynamic configuration, unsupported peers and reentrancy.
Windows `scripts/test-windows-runtime.ps1 -Arguments @('--validate-popup')` and
Android `-Mode Popup` exercise real native actors,
target DOM/string delivery, actual HTTPS handoff, independent user-agent/scheme
settings, lifetime, default/gesture filters, reuse and script close. An old helper
actually attempts a send after JS disable/re-enable; the bounded no-delivery result
is distinct from observing a rejected live native callback. Their exact
results and platform limits belong in the PR qualification report, not inferred from
compilation. API23 baseline and broader focus/Designer limits remain recorded in
[platform qualification](native-platform-qualification.md).
The Windows validator owns profile cleanup after the entire process tree exits;
the original in-process deletion race is retained as failed development evidence.

See the [complete options roadmap](security-options-roadmap.md) for deferred scopes.

SDK references: [WebView2 target requirements](https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2newwindowrequestedeventargs.newwindow),
[Android popup implementation](https://chromium.googlesource.com/chromium/src/+/HEAD/android_webview/docs/how-does-on-create-window-work.md),
[Android WebView Java interface/load and visual-state APIs](https://developer.android.com/reference/android/webkit/WebView),
[API23 string ports and ownership](https://developer.android.com/reference/android/webkit/WebMessagePort).
