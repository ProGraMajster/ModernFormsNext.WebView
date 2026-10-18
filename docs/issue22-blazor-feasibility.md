# #22 — .NET 10 Blazor feasibility ADR (2026-10-18)

Delivery update: #42 was normally merged after its [final audit](pr41-43-final-audit.md).
Then-open wording describes the original research delivery. Its default-profile NO-GO
is preserved. The explicit [application-content policy](local-content-policy.md) permits
a separate controlled assets experiment; it does not authorize managed page attachment.

## Decision

**NO-GO for a full interactive BlazorWebView using the current public content/security
contract.** Do not start #23–#31 on the strength of passive rendering or a constructor
test. The executable Windows prototype demonstrates real compiled Razor rendering,
scoped DI, lifecycle, native MFN controls and public WebViewManager seams, then measures
the blockers to interactive startup. No unverified C# handler dispatch is enabled.

This is an executable research deliverable, not a new empty product control. The
[project and commands](../research/BlazorFeasibility.Windows/README.md) remain isolated
outside the product solution. Baseline WebView release gates do not depend on this ADR.

## Verified public APIs and prototype architecture

[WebViewManager](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.webview.webviewmanager?view=aspnetcore-10.0)
is a public platform-author seam. The concrete research subclass implements NavigateCore
and SendMessage with the existing WebView/transport, tests TryGetResponseContent against
immutable catalog bytes, registers/removes a root component, and verifies TryDispatchAsync
does not expose a page scope before a qualified attachment. Its protected inbound
MessageReceived path is deliberately unavailable to baseline untrusted events.

MfnComponentDispatcher maps ASP.NET's four InvokeAsync overloads/CheckAccess to MFN's
existing UI dispatcher; native validation invokes it from a background caller and checks
actual UI access. It creates no thread, native host or app message pump. HtmlRenderer
uses the standard Razor rendering context before the native loop, not a parallel MFN UI.

RazorSnapshots renders an actual SDK-compiled .razor component through public HtmlRenderer,
with parameters, fresh scoped services and deterministic component/scope disposal. Native
MFN Button and Label coexist with the existing WebView. The button selects the next
separately rendered immutable HTML snapshot; DOM values/reload are verified. This proves
passive Razor/native cooperation, not an interactive Razor event circuit.

CatalogFileProvider adapts the existing public WebViewLocalContent response API, exposing
no physical paths, directory traversal, filesystem watcher or native request server.
The browser still uses the canonical immutable byte catalog and its unchanged policy.
Manager asset responses are tested through supported APIs, not native/private reflection.

The standalone service provider contains only logging, Blazor registrations and probe
state/counters, not application services or a service locator. IJSRuntime resolution is
tested; invocation before a page attaches is rejected. Native baseline scripting remains
separate from unqualified Blazor interop. No application [JSInvokable] methods are added.

## Measured blockers

The pinned official .NET10.0.12
[Boot.WebView](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Components/Web.JS/src/Boot.WebView.ts)
calls the initializer loader before attaching a page. That
[loader fetches its manifest](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Components/Web.JS/src/JSInitializers/JSInitializers.WebView.ts)
from `_framework/blazor.modules.json`. The prototype supplies the real manifest/JavaScript;
the public response tests confirm asset bytes exist. In a real WebView2 document the
unmodified Blazor.start fails with a positive **connect-src CSP violation**. Existing
WebViewLocalContent intentionally sets connect-src 'none'. Its base-uri 'none' also
rejects the supplied base element, verified against the real document baseURI.

No fetch shim, runtime-JS patch, alternate permissive server, CSP/TLS weakening or private
MAUI code is used to make startup appear successful. Fixing this requires a separately
designed content contract and qualification, not changing the baseline policy here.

Independently, native Blazor IPC can trigger managed operations. The stock manager's
[source/base check](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.12/src/Components/WebView/WebView/src/WebViewManager.cs)
does not authenticate an original same-origin script. #16/#15 already demonstrate parent
relay. The prototype submits a claimed AttachPage message through the real current helper;
the event remains untrusted and is rejected. No page context/privileged DI dispatch attaches,
and AuthenticatedMessaging stays absent. A working script load is not a security approval.

## Proposed future architecture and gates

1. Optional separate Blazor package/project depending on baseline WebView and the public
   Microsoft API. No ASP.NET dependency in ordinary WebView, templates or main framework.
2. An explicitly designed trusted application-content capability supporting the required
   immutable same-origin asset fetches and correct base URI, while excluding untrusted
   frames/scripts/workers/navigation. Review this separately from baseline public bytes;
   include manifests, component modules, routing/fallback, bounds and exact asset ownership.
3. A verified whole-application-realm trust contract and platform channel, distinct from
   identifying a particular caller script. Untrusted content belongs in a separate
   unbridged view. A token, ID or Source is not sufficient. #15/#17/#13 remain gates.
4. Canonical document/peer cancellation owns page scopes and manager/renderer disposal.
   Component, interop and navigation callbacks must reject stale generations before
   returning to MFN's dispatcher. No second host/focus/layout/dispatcher lifecycle.
5. Only after those gates, implement/test interactive root components, IJSRuntime and
   navigation/DI lifetime. Android needs its own provider/API/runtime evidence. Nothing
   here authorizes full #23–#31 or an optional package release.

## Dependencies, licensing and qualification

Research pins [Microsoft.AspNetCore.Components.WebView10.0.12](https://www.nuget.org/packages/Microsoft.AspNetCore.Components.WebView/10.0.12)
and DI10.0.12, with actual package assets inspected. The package is MIT licensed and carries
third-party notices. Its unmodified JS/modules are embedded from NuGet during build;
research output includes the ASP.NET license and package notices. No SDK source is vendored,
no MAUI internals copied, and no ASP.NET server/runtime is added to the base package.

The downloaded official NuGet archive SHA256 is
`86D88966480750DA60C40D3D08EA8BABDDA69B894622592A8B605C9242E74E78`.
The ordinary Windows sample's resolved graph contains zero
Microsoft.AspNetCore.Components packages after the research restore/build.

## Executed validation

On .NET SDK10.0.401/runtime10.0.12, Windows SDK1.0.4258.31/runtime155.0.4283.62
(native product logs in both configurations; earlier #19 used155.0.4283.60):

| Lane | Result |
| --- | --- |
| Research Debug and Release build | PASS, zero warnings/errors |
| Research contract mode, both configurations | PASS compiled Razor/parameters/scoped DI/disposal/public manager/assets/unattached interop rejection |
| Research native mode, both configurations | PASS passive DOM/native controls/dispatcher/reload/CSP and IPC rejection/process-tree profile cleanup; interactive NO-GO |
| Full product solution Debug and Release | PASS, zero warnings/errors, 207/207 tests each (158 shared, 37 Windows, 12 Android), zero skipped |
| Product Windows and ordinary Android native, both configurations | RUNTIME PASS; Android only emulator-5554, API34/provider113.0.5672.136 |
| Local self-contained win-x64 publish, no trimming | PASS, zero warnings; published contracts and native probe PASS |
| Local partial-trim publish, default feature switches | Compilation succeeds with 193 warning log lines; contracts PASS; native initialization FAIL |
| Same partial trim with explicit BuiltInComInteropSupport=true | Compilation succeeds with 194 warning log lines; published contracts/native probe/profile cleanup PASS; trimming remains UNQUALIFIED |
| Local NativeAOT attempt | 135 warning log lines, then FAIL: platform linker unavailable (Desktop Development for C++ workload); no executable/runtime qualification |

These were local executable publish diagnostics, not package/release publication. Counts
are warning log lines from each run, including compilation/link analysis; they are not
unique defects or directly comparable performance measurements. No warnings were suppressed.
Trim diagnostics include reflection/TypeDescriptor/backend discovery, COM marshalling and
ASP.NET/JSInterop/WebView2 assembly warnings. The default trimmed runtimeconfig disables
built-in COM support despite the WebView2 loader being present. The command-line-only
COM-enabled variation isolates that startup obstacle; a bounded native PASS does not
qualify general trimming or prove that removed metadata/COM paths are safe.
[The documented trimming switches](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)
explain the feature default. No framework/source fixes or support promises were introduced.

Commands: pinned bootstrap/source root; full solution restore and sequential build/test in
Debug/Release with shared compilation/package generation disabled; ordinary Windows
`--validate`, signed Android APK and explicit-serial validation Intent. Research commands
are in its README. Publish diagnostics used `dotnet publish -c Release -r win-x64
--self-contained true`, then respectively `PublishTrimmed=false`,
`PublishTrimmed=true/TrimMode=partial`, the same plus `BuiltInComInteropSupport=true`,
and `PublishAot=true`. Successful outputs ran `--validate-contracts` and the same native
validator with `-ExecutablePath`. Local evidence is under ignored artifacts/issue22 and
artifacts/issue22-product; PR/hosted exact-head metadata is recorded with delivery.

NOT EXECUTED: interactive Blazor JS interop/event rendering, routing/deep-link/hot reload,
Android Blazor, API23 runtime, physical device, foreground keyboard/UIA/manual visual,
Designer/full IME/DPI/accessibility, retained-heap soak, full trimming or NativeAOT runtime.
API34 is not API23 evidence. Native startup/DI/static rendering does not qualify these lanes.

An initial fixture tried deleting a profile while the application-owned environment could
still exist; waiting only for the parent process also left seed files with sharing violation
0x80070020. The validator now waits for the owned process tree using documented
[Start-Process -Wait semantics](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/start-process?view=powershell-7.5),
then cleans only its verified artifact profile. The cleanup retry bound is unchanged;
no other app/process is terminated. Final-run results and any earlier retained fixtures
are reported separately. Final fresh Debug/Release and successful published probes clean
their own profiles. Two early OS Temp fixture directories remain: automatic approval review
rejected their manual deletion with "blocked by policy" and no further reason. No alternate
deletion API was used. Two early failed artifact profiles are retained as diagnostics.
This is not retained-heap leak proof.

## Delivery

Open [PR #42](https://github.com/ProGraMajster/ModernFormsNext.WebView/pull/42), implementation
`72a3aa0`, based on verified #41 for sequential review. It remains open; no auto-closure
or permission to implement #23–#31. Exact final delivery HEAD and hosted CI are recorded
in the PR separately from local runtime evidence above.
