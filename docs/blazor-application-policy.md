# Blazor application-policy experiment (#22)

This follow-up separates **A: native loading of original .NET10 assets** from
**B: authorized IPC/interactivity**. A successful boot promise does not mean a managed
page, render circuit or scoped service is attached. The earlier
[default-policy experiment](issue22-blazor-feasibility.md) remains valid.

## Explicit selection and unchanged assets

The existing research executable accepts --application-policy; its validator accepts
-ApplicationPolicy. Without it the catalog still uses the exact default CSP. With it,
only that immutable catalog uses WebViewLocalContentPolicy.Application: connections
and base URI change from none to self. Scripts/styles/images retain self; inline/eval,
external origins, frames and workers remain denied. The [policy ADR](local-content-policy.md)
contains the complete option/default/effect/risk/support table and examples.

No package script is edited, no fetch is replaced and no synthetic manifest is used.
The official pinned Microsoft.AspNetCore.Components.WebView10.0.12 assets are embedded
from NuGet at build time, unchanged. The native probe checks the real base URI, fetches
the original _framework/blazor.modules.json (200, JSON array and exact CSP header),
starts the original public Blazor.start() and observes its real AttachPage request
before sending a forged test request. Default mode still positively observes CSP denial.

## Trust and managed lifetime

Both AttachPage requests are counted only as bounded **untrusted diagnostics**, then
rejected. The manager's protected MessageReceived is never invoked. TryDispatchAsync
must return false; no managed page scope, native operation, service, reflection or file
handler becomes reachable. ConfigurableLocalContent is independent of
AuthenticatedMessaging, which remains absent; IsOriginAuthenticated remains false.

Contract mode exercises both catalogs, the public manager navigation/assets/root
registration API, pre-attachment IJSRuntime rejection, compiled static Razor and scoped
DI/component disposal. Native mode delegates to the existing MFN dispatcher and host,
then tests static DOM, native button snapshot replacement and reload. It deliberately
does not execute a Razor event circuit, routing or JS interop after page attachment:
the required channel authorization has not been demonstrated.

| Gate | Decision and scope |
| --- | --- |
| A: base, self manifest, original boot | GO in native WebView2 Debug run; no managed acknowledgement implied |
| B: authorized AttachPage/native IPC | NO-GO; all messages rejected |
| Interactive Razor/DI/IJSRuntime/routing | NOT EXECUTED; blocked by B |
| Public dispatcher/static renderer/disposal | Tested separately; no proof of interactive lifetime |
| Android Blazor | NOT EXECUTED; Android baseline policy is separately tested |
| Trimming/NativeAOT | No new qualification; earlier trim/COM/linker limits remain |
| Overall interactive Blazor | **NO-GO**; #22 stays open, no #23–#31 implementation |

## Reproduction and evidence

Build the existing research project as described in its README, then run each Debug
and Release configuration with separate evidence folders:

    ./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug -EvidenceDirectory artifacts/blazor-default
    ./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug -ApplicationPolicy -EvidenceDirectory artifacts/blazor-application

The validator waits for its owned process tree and verifies cleanup of its unique profile.
It does not change other WebViews or profiles. CI runs contracts; native UI evidence is
local and separate. No product ASP.NET dependency, framework pin change or publication.

The first native application-policy run used SDK10.0.401, WebView2 SDK1.0.4258.31 and
runtime155.0.4283.62, with profile cleanup PASS. Final Debug/Release regression lanes
and exact delivery-head CI are recorded on the separate research PR. This branch is
stacked on verified policy PR #47 (140fafe; both hosted runs terminal SUCCESS); it must
remain open. Full IME/DPI/accessibility/Designer/heap, API23/physical device, privileged
realm qualification and new full trim/AOT execution are not claimed.

The public [Boot.WebView.ts](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Components/Web.JS/src/Boot.WebView.ts)
sends AttachPage without waiting for a managed attachment acknowledgement.
The [IPC sender](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Components/Web.JS/src/Platform/WebView/WebViewIpcSender.ts)
uses window.external.sendMessage. The
[public manager source](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Components/WebView/WebView/src/WebViewManager.cs)
creates the managed page after receiving IPC. Its source-URI check does not authenticate
the original caller script.
