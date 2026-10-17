# #12 — versioned, unprivileged app-to-page RPC

This dependent stage starts from #11 final head
`9b03ae8d3b08e2d4f2d4196758dd31a4d8d38a39`, after terminal SUCCESS in
[PR CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39044791926)
and [push CI](https://github.com/ProGraMajster/ModernFormsNext.WebView/actions/runs/39044788970).
#36 and #37 remain unmerged. This branch targets #37's branch for a focused diff.

`WebViewRpcClient` borrows `IWebViewTransport`. It sends only explicit application
requests to allowlisted **JavaScript** functions. It never exports C# methods, objects,
files, services or a handler registry. Inbound requests/cancels are ignored by C#.
Nothing installs automatically and ordinary WebView/script/string APIs keep their behavior.
All APIs remain experimental; no package, stable contract or platform parity is promised.

## Use

On the owning UI thread, after successful document completion and explicit JavaScript opt-in:

```csharp
using var transport = new WebViewTransport(browser);
string installed = await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript);
if (installed != "true") throw new NotSupportedException("Page RPC features unavailable.");
await transport.ExecuteScriptAsync("modernFormsRpc.register('echo', value => value);null");
using var rpc = new WebViewRpcClient(transport);
string? reply = await rpc.InvokeAsync("echo", "hello",
    AppJson.Default.String, AppJson.Default.String,
    TimeSpan.FromSeconds(5), cancellationToken);

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal partial class AppJson : System.Text.Json.Serialization.JsonSerializerContext;
```

JsonTypeInfo arguments are required. Use source-generated metadata for all concrete
payload types, with application-approved converters. There is no reflection-based method
discovery or automatic JSON reflection fallback in this layer. Explicit metadata enables
AOT/trimming-compatible serialization; a full published trimmed/AOT application has **not**
been qualified here. Typed deserialization can execute an application-supplied converter;
that converter is trusted application code and must validate untrusted values itself.
Request serialization starts on the invoking UI thread. Response conversion can run
off-thread; converters must not access controls or rely on a UI synchronization context.

The page can register `(payload, AbortSignal) => value | Promise` via
`modernFormsRpc.register(name, handler)` and remove it with `unregister(name)`.
Duplicate registrations are errors, the registry is bounded to 64 methods, and no arbitrary
global function name is evaluated. Return JSON-compatible data. Missing/undefined/cyclic
or excessively deep results use `invalid_result`; oversized results use `result_too_large`.
Thrown/rejected page exceptions become `handler_error`, without their text or stack.

## Wire v1

Every message is one baseline string containing exactly one JSON object:

```json
{"version":1,"type":"request","id":"0123456789abcdef0123456789abcdef","document":"1:7","method":"echo","payload":"hello","timeoutMs":10000}
{"version":1,"type":"result","id":"0123456789abcdef0123456789abcdef","document":"1:7","payload":"hello"}
{"version":1,"type":"error","id":"0123456789abcdef0123456789abcdef","document":"1:7","error":{"code":"handler_error"}}
{"version":1,"type":"cancel","id":"0123456789abcdef0123456789abcdef","document":"1:7"}
```

Envelope properties are exact: missing, duplicate (including escaped names), unknown,
incorrectly typed fields and unsupported versions are rejected. C# replies additionally
require a known pending ID, matching wire document and original/current canonical transport
context. Result payloads are cloned before the parser document is disposed. First valid
terminal reply wins; unknown, duplicate, malformed and stale replies are ignored. Unknown
versions are ignored until the caller deadline, rather than generating unsolicited traffic.

| Bound | Policy |
| --- | --- |
| Wire size | 65536 UTF-8 bytes including the envelope; UTF-16 length precheck |
| JSON depth | 32 container levels, request and response |
| Pending calls | 64 per C# client and 64 per page endpoint |
| Method | 1–128 ASCII letters/digits/underscore/dot/hyphen |
| Request ID | Fresh 32 lowercase hex GUID string per invocation |
| Document | Invariant decimal `PeerGeneration:RequestId` string, preserving 64-bit IDs |
| Deadline | Default 10000 ms; explicit 1–120000 ms; includes transport submission |
| Page duplicate cache | Pending IDs plus the 256 most recent accepted IDs, bounded per document |

The page error code allowlist is `method_not_found`, `handler_error`, `busy`, `timeout`,
`invalid_result`, `result_too_large`. Other error data is ignored. `WebViewRpcException`
contains a fixed category/message and optional allowlisted RemoteCode, with no payload,
page/native exception details, inner exception, URI, profile or secret logging. Response
type conversion failures become InvalidResponse; serialization/size/depth failures become
InvalidPayload. Transport failures use Transport. Malformed replies do not complete a call.

## Cancellation, ownership and threading

Invoke, subscribe and dispose on the transport's UI thread. No work is dispatched to
Task.Run. A bounded pending map correlates requests; no host, focus system, document
counter or dispatcher is introduced. Continuations can complete off-thread; a lock protects
pending-map changes and completion sources run continuations asynchronously. No native
or application callback runs under that lock.

Caller cancellation, canonical document cancellation, transport disposal and client disposal
cancel local waits even if native submission never completes. The deadline separately bounds
both submission and response. Cleanup removes pending state on all completion paths.
Disposing the client does not dispose its transport or browser; disposing the transport
does not dispose its browser. There are no automatic retries or replay of requests.

A local timeout is a sanitized Timeout exception; caller/document/lifetime invalidation
uses OperationCanceledException. A page timeout uses the fixed timeout code/category.
Best-effort cancel posts through the existing UI dispatcher, only after rechecking the
original document. A replacement document never receives the old cancel. A custom transport
without a live MFN dispatcher still settles locally, but advisory delivery is not promised.
The page aborts its signal, clears its timer/pending state and ignores late results.
Handlers must cooperate; cancellation cannot undo side effects or stop synchronous JavaScript.
pagehide retires subscriptions, methods, pending controllers, timers and the endpoint global,
including for back/forward-cache restoration. Each new/reloaded/
recreated document requires explicit installation/registration again.

## Security and platform limits

IDs and document strings are correlation, not secrets or caller authentication. Replies
remain untrusted even when they match an outstanding request. A compromised page, same-origin
iframe using its parent or Android frame calling AddJavascriptInterface can forge data.
Never authorize privileged C# actions or send secrets based on this result. The current
Android bridge supplies no authenticated origin. Neither adapter advertises AuthenticatedMessaging.
#13/#15/#17 remain gated; a useful protocol foundation does not satisfy that security gate.

The explicit endpoint requires Promise, Map, Set, AbortController and TextEncoder in the
installed engine. Platform API23 alone does not guarantee those JavaScript features.
Installation returns false when required features are missing. No AndroidX binding,
polyfill, CSP weakening, TLS/cleartext bypass or new dependency is introduced.
Windows uses WebView2 strings; Android uses the existing JSON-escaped native script path.
The page endpoint is ordinary unprivileged JavaScript, reachable by other page scripts.
Size/depth/pending bounds protect the protocol layer, not arbitrary native-page memory or CPU.

## Validation

Deterministic tests cover explicit typed metadata, synchronous and out-of-order replies,
duplicates, versions/types/fields/escaped duplicate keys, UTF-8 size/depth, unsolicited
requests/errors, current/stale document contexts, type errors, sanitized errors, deadline
during submission/response, every cancellation lifetime, borrowed ownership, readiness,
method/deadline validation and pending-capacity recovery.

The shared native fixture installs the real endpoint in both engines and checks JSON
escaping, asynchronous out-of-order results, unknown/thrown methods, timeout, a real page
AbortSignal, malformed/version/duplicate/oversized envelope rejection, document replacement,
no implicit injection and borrowed transport compatibility. Full baseline/security/transport
fixtures run afterwards as separate regression coverage. Final counts and CI follow execution.

## Execution report — 2026-10-17

- Unchanged pinned bootstrap and solution restore PASS.
- Full sequential Debug/Release solution builds: 0 warnings/errors; tests 166/166 each
  (139 shared, 15 Windows, 12 Android policy), zero skipped.
- Windows Debug/Release native `--validate`: RUNTIME PASS and successful process exit.
  WebView2 runtime `155.0.4283.60`; full baseline/security/transport/RPC/navigation/lifetime
  fixtures, 50 browser leases and reparent/replay.
- Android Debug/Release signed APK builds/install/native: full RUNTIME PASS on explicitly
  selected emulator-5554, API34 x86_64, `com.google.android.webview/113.0.5672.136`.
- Commands: pinned bootstrap; solution restore; `dotnet build -c Debug/Release --no-restore
  -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false`; solution
  `dotnet test --no-build --no-restore -m:1`; all with source-root/Windows targeting;
  Windows sample `--validate`; Android `-t:SignAndroidPackage`/JDK21, explicit emulator
  install/validation Intent and PID-filtered logs. Portable invocations: [testing](testing.md).

Final review added pagehide/global retirement and explicit reinstall/unsupported-feature
native cases, exact byte/depth boundary tests and cancellation during response conversion.
Cancellation remains the terminal outcome even if an application converter both cancels
and throws, on request serialization or response conversion. The final full Debug/Release
and both native-platform runs above include these refinements.

NOT EXECUTED: physical device, API23 runtime, foreground keyboard injection, interactive
Visual Studio Designer, full IME/DPI/accessibility/retained-heap soak, published trimming/AOT
qualification. Template verification is not applicable. No native authentication capability,
C# handlers, framework/pin changes, new dependencies, packages or releases. The dependent
PR/exact final-head hosted results are tracked separately from this local execution evidence.
