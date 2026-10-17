# #11 — reusable untrusted browser transport

Branch `work/issue-11-transport` is based on #33 head
`5466e712ed7bd276420a8374c105f3a878293f39`. Its
[PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043213480)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39043209196)
were terminal SUCCESS before this stage began. The dependent PR targets the #33 branch,
so only this layer is reviewed; neither follow-up is automatically merged.

`WebViewTransport : IWebViewTransport` borrows a WebView and forwards the existing
ExecuteScriptAsync/PostMessageAsync/WebMessageReceived operations. It changes no
baseline API semantics or engine script scope/JSON projection. No scripts are installed
automatically. It owns only one cancellation source and event subscription, not the
browser/provider/native peer. Dispose on the UI thread; it cancels its pending calls and
unsubscribes while other transports and the baseline control remain usable.

```csharp
// Own this transport for as long as the application needs its subscription.
using var transport = new WebViewTransport(browser);
transport.MessageReceived += (_, e) => ValidateUntrustedData(e.Message);
await transport.PostMessageAsync("hello", cancellationToken);
string json = await transport.ExecuteScriptAsync("({ answer: 42 })", cancellationToken);
```

CurrentDocument is an immutable snapshot of the control's original RequestId,
PeerGeneration, document CancellationToken and completed-document readiness. Obtain
it on the UI thread, then inspect the snapshot or observe cancellation elsewhere.
The existing control owns these values: there is no second epoch counter, host,
dispatcher or navigation observer tree. Navigation/Stop/JS disable/retirement/dispose
cancel the same canonical token as baseline operations. IsReady additionally requires
successful completion with JS enabled; after Stop/canceled navigation, reload before
starting a document-dependent protocol. It is not an authentication or source allowlist.

Forwarding compares original event metadata with the current canonical context. A
preceding baseline observer may navigate or dispose; that original event is dropped
rather than relabeled. Within transport observers, the immutable snapshot stays original
and its token records any reentrant invalidation. IsOriginAuthenticated stays false.
Source is never consulted for authorization.

Invoke operations, subscribe, inspect CurrentDocument and dispose on the owning UI
thread. Argument/access/disposed checks are synchronous; readiness/native errors use
the existing operation exception categories through the returned task. Results can
complete off-thread. LifetimeToken remains readable/canceled after transport disposal.
Caller/transport cancellation settles pending waits without obsolete native completion;
already submitted JavaScript effects cannot be rolled back. Posts acknowledge submission,
not receipt; the native sample uses an explicit page acknowledgement.

Validation covers independent wrappers, borrowed ownership, JSON/string compatibility,
caller/dispose cancellation, canonical retirement/replay, original metadata, reentrancy
and unsubscription. Headless UI scopes are disposed before awaiting detached task
outcomes, preserving the existing strict test-host thread affinity. Native fixtures
exercise JSON/escaping, acknowledgement, replacement invalidation and disposal without
disabling the baseline. Final execution counts and exact-head CI are recorded with delivery.

The full Android run exposed an existing POST observer-reentrancy race: OnPageStarted
had not marked the native load active before invoking NavigationStarting observers.
A replacement raw document could reuse that View, letting a late POST finish complete
the replacement prematurely and revoke its next script. The adapter now marks the
already-started load active before observers, so its existing interrupted-load path
retires the old View. The unchanged native POST/replacement/DOM assertion is the
regression check; cancellation is not swallowed or retried by the fixture.

Windows Release reached RUNTIME PASS but failed profile cleanup in two runs. The sample
now explicitly disposes its Form/control tree after Application.Run and before cleanup;
previously its using declaration disposed that tree only after the cleanup attempt.
The subsequent Release run completed successfully. The cleanup retry count/time budget
and failure exit code are unchanged; no browser process was killed to obtain that result.

No authenticated bridge, C# handlers, RPC protocol, dependencies, pin/framework changes,
templates or publication are introduced here. RPC in #12 consumes this layer separately.

## Execution report — 2026-10-17

- Bootstrap/solution restore PASS at unchanged framework pin `69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`.
- Sequential full Debug/Release solution builds: 0 warnings/errors; tests 128/128 in
  each (101 shared, 15 Windows, 12 Android policy), no skips.
- Windows Debug/Release `--validate`: complete RUNTIME PASS, including transport,
  baseline, security, POST, 50 leases and reparent/replay; final process exit success.
- Android Debug/Release signed APK build/install and full native RUNTIME PASS on
  emulator-5554, API34 x86_64, provider `com.google.android.webview/113.0.5672.136`.
- Commands: `scripts/bootstrap-framework.ps1`; solution `dotnet restore`, sequential
  `dotnet build -c Debug/Release --no-restore -m:1 -p:UseSharedCompilation=false
  -p:GeneratePackageOnBuild=false`, `dotnet test --no-build --no-restore -m:1`, each
  with `ModernFormsNextSourceRoot`/Windows targeting; Windows sample `--validate`;
  Android sample `-t:SignAndroidPackage` with JDK21, `adb -s emulator-5554 install -r`,
  validation Intent and PID-filtered MFNWebView logs. See [testing](testing.md).

Physical/API23 runtime, foreground keyboard injection, Visual Studio Designer,
full IME/DPI/accessibility and retained-heap soak: NOT EXECUTED. No template changes
or template verification needed. Native tests are separate from hosted CI; no release
or stable API guarantee is claimed. Delivery/exact-head checks are linked by the PR.

Delivered as [PR #37](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/37),
implementation `efccb3c`, targeting #36's branch and intentionally unmerged.
