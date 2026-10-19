# Android adapter

Experimental [new-window handoff](new-window-policy.md) uses the API23 platform
WebViewTransport and a fresh target in the same Activity. Android supplies no URI or
frame/origin authentication. Initial blank-popup readiness uses a guarded native
visual-state callback; ordinary URL callbacks stay canonical. No AndroidX/minSdk
change or new manifest permission is introduced. Deny remains default.

The baseline still targets API23. The optional [navigation scheme filter](navigation-scheme-filter.md)
requires API24+ main-frame request metadata; API23 explicitly rejects nondefault settings/
loads. POST fallback runs after network start and cannot guarantee no server effects.
The API34/provider113 evidence does not qualify API23/physical devices, and no scheme
selection changes cleartext/mixed-content policy or authenticates AddJavascriptInterface.

The native target is net10.0-android, minSdk **23**, matching the main backend. The adapter
uses platform Android.Webkit.WebView; it adds no AndroidX WebKit, MAUI, Essentials or wrapper
package. Java.Interop.Export/JavascriptInterface come from the .NET Android platform SDK.
The sample retains the main sample's HarfBuzzSharp **14.2.1.1** override for 16 KiB-compatible
transitive native assets. This does not modify the main framework.

Initialize AndroidWindowKit in Application.OnCreate, then AndroidWebViewProvider.Register.
Registration before backend initialization throws a clear exception without resolving a dispatcher.
Derive the Activity from AndroidWindowActivity and start `Application.Run(new MainForm())`
from OnStartApplication. The backend owns the Activity/root/message-loop lifecycle. The sample
has only INTERNET permission and usesCleartextTraffic=false; offline raw HTML needs no network.

Each session creates a parentless fresh WebView using AndroidNativeViewSite.Context and attaches
with AndroidViewPeer. Native content uses existing container size/focus APIs. Tab traversal delegates
directly to WebView's native key handling because HTML focus nodes are not Android View children;
the site owns leaving the browser when native handling declines. Activity replacement
destroys A and creates B from the fresh site; no old Activity context or View is migrated.
The same shared control replays its last URI/raw HTML. Loading/history reset; no SaveState/RestoreState
browser session guarantee is made. Background/resume uses the existing presentation visibility policy.

On overlapping programmatic navigation, Android's callbacks lack native request IDs. The adapter
revokes and destroys the interrupted browser content instance in the same framework container
before creating fresh content. This resets history for that case, rather than attributing an old
callback to the latest request. The same retirement is required before the next explicit
navigation after a failed load or Stop during loading: Android can still queue an error-page
Started/Finished callback after reporting a certificate error. The flag is committed before
failure observers may recover synchronously. Such recovery resets history too. Stop on an
already completed page does not by itself require native browser replacement. Completed
sequential navigation uses the normal native history. This is not a claim that native
callbacks identify documents or that every history/reload-after-failure path is qualified.
Generation guards also apply to the old WebViewClient and bridge; no second host lifecycle is created.

Programmatic URI/raw HTML requests receive a shared cancelable preflight before native loading.
Modern ShouldOverrideUrlLoading is restricted to main-frame requests; API23 uses the legacy hook.
The SDK does not call that hook for every navigation (notably POST, some same-document/engine paths).
OnPageStarted is an additional notification/stop fallback, not a security-grade pre-network veto.
A POST starting after the previous page completed receives a fresh logical identity and exposes
its actual URI, including when it leaves raw HTML. Reentrant handlers cannot stop a newer request.
IsRedirect is the adapter's classification of an ongoing load, not authenticated navigation intent.
Redirect callback counts differ from Windows. Read
[Android WebViewClient](https://developer.android.com/reference/android/webkit/WebViewClient).
Main-frame WebResourceError reports failures; subresource errors are ignored. SSL errors always cancel,
and only a matching main-document address reports a top-level failure. Completion has no reliable
success HTTP status field. Android diagnostics use provider package/version on API26+; older API23
may report an unknown version.

The fixed private bridge exposes one annotated string method. It exists only with JS enabled, has
no sender/frame/origin authentication, and receives data from all frames. Messages carry a logical
document token to reject obsolete callbacks, not to authenticate content. Payloads are still untrusted.
Malformed, non-string and null transport packets are discarded; token types are checked before parsing.
Only strings are delivered on the MFN dispatcher thread. The helper is installed at raw HTML startup
or remote completion. App messages use JsonSerializer escaping and EvaluateJavascript MessageEvent.

Dispose removes the interface, detaches clients, stops loading, detaches content, calls Destroy and
Dispose, releases client/bridge references and cancels pending evaluations. It does not clear global
cookies/cache. JNI callback wrappers can survive until Chromium releases its outstanding callback;
their canceled tasks and removed pending entries cannot update the retired shared control.

The sample's validate Intent extra runs common scenarios and an additional HTTPS/history sequence.
Rotation intentionally recreates its Activity (no Orientation ConfigurationChanges bypass). Report
API/ABI/provider/density, ordinary recreation and background/resume separately from physical device,
API23, long soak, IME and retained-Activity profiler evidence.
