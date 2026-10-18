# .NET 10 Razor / WebViewManager feasibility (#22)

An isolated Windows research executable, outside the product solution. It uses the
existing WebView control/provider/NativeViewHost and public ASP.NET Core10.0.12 APIs.
No MAUI implementation is copied, and no second native host, UI loop or layout/focus
service is created. ASP.NET dependencies belong only to this project.

**Interactive BlazorWebView: NO-GO under the current IPC/security contract.**
The runnable prototype shows compiled passive Razor output beside native MFN controls;
it also attempts unmodified `Blazor.start()` and positively records CSP denial. It does
not advertise a full interactive host, authenticated IPC or C# handler registry.

```powershell
$framework = & ./scripts/bootstrap-framework.ps1
dotnet build ./research/BlazorFeasibility.Windows -c Debug -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework
dotnet run --project ./research/BlazorFeasibility.Windows -c Debug --no-build -- --validate-contracts
./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug
./research/BlazorFeasibility.Windows/Validate.ps1 -Configuration Debug -ApplicationPolicy -EvidenceDirectory artifacts/blazor-application
# Repeat Release. Launch without arguments for the native passive-snapshot demonstration.
```

Contract mode executes compiled Razor parameter rendering, fresh scoped DI/component
disposal, public WebViewManager resource/navigation/root registration and rejection of
IJSRuntime calls before page attachment. Hosted CI runs this mode without native UI.
Native mode verifies MFN dispatcher delegation, actual browser DOM, package JavaScript,
connect-src/base-uri enforcement, rejected untrusted IPC, native button snapshot changes
and reload. A native button selecting a separately rendered static snapshot is not an
interactive Razor event circuit. UIA/physical keyboard/manual visual tests are separate.

The validator owns a unique profile under ignored artifacts/blazor-profiles and waits
for the **whole owned process tree** before cleanup. Waiting only for the .NET parent
left WebView2 seed files briefly locked. It never stops other applications or deletes
their profiles. The cleanup retry bound is unchanged.

The resource adapter exposes only existing immutable WebViewLocalContent bytes to the
public IFileProvider seam. No physical directory, watcher, server, TLS exception or CSP
override is exposed. Explicit application-policy mode uses the typed catalog policy
to permit only self connections/base; it does not grant privileges. Native messages remain untrusted and never reach the manager's
protected privileged MessageReceived dispatch. The manager's built-in Source check is
not accepted as original-script authentication.

Official package JavaScript is embedded unchanged at build time from the pinned NuGet
asset; no runtime download or vendored SDK implementation. ASP.NET Core is MIT licensed;
the license and package THIRD-PARTY-NOTICES are copied to output/publish. No package or
release is published. [ADR/results](../../docs/issue22-blazor-feasibility.md) distinguish
implemented seams, failed interactive startup and unexecuted qualification.

The [application-policy follow-up](../../docs/blazor-application-policy.md) separately
qualifies original asset startup and rejects the real AttachPage. Asset GO does not
mean authorized IPC or interactive GO. Default mode retains its positive CSP denials.

Debug/Release research build/contracts/native and full product 207/207 tests/native lanes
pass. Local self-contained win-x64 without trimming also passes the published probe.
Default partial trim builds with warnings but fails native initialization; explicitly
preserving built-in COM enables this bounded probe, still with unresolved trim warnings.
NativeAOT cannot link on this machine because the C++ linker workload is unavailable.
Neither trimming nor NativeAOT is qualified. See the ADR for exact diagnostics and limits.
