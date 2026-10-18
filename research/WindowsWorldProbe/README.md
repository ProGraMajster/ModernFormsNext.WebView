# Bounded Windows execution-world experiment

This internal executable uses the existing WebView/NativeViewHost, typed Windows site,
MFN dispatcher and canonical transport document token. It does not register a product
provider or expose CDP through product APIs. The minimal SDK peer is a research fixture;
it is not a second hosting/layout/focus/dispatcher system or a replacement backend.
The existing product acknowledgement queue is source-linked unchanged; the client alone
allocates logical request IDs. The sample's loopback server is also reused unchanged.

Only bounded diagnostic counters are reachable from the isolated-world binding.
No operations, DI, reflection, files or service/handler registry is attached. Product
AuthenticatedMessaging and IsOriginAuthenticated remain unchanged/false.

    $framework = & ./scripts/bootstrap-framework.ps1
    dotnet build research/WindowsWorldProbe -c Debug -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework
    ./research/WindowsWorldProbe/Validate.ps1 -Configuration Debug
    # Repeat Release. Hosted CI compiles; native execution is local and explicit.

The validator owns a unique artifact profile, waits for the whole process tree and
verifies profile cleanup. It never stops other applications or deletes their profiles.
The raw HTML fixtures deliberately have **no CSP** and remain unprivileged. The experiment
does not weaken a local-content policy, qualify isolated-world CSP or prove a trusted
whole-app realm. No universal access, setBypassCSP, browser flags, remote debugging port,
TLS bypass or global binding fallback is used.

[ADR and matrix](../../docs/windows-world-experiment.md) record the measured scope.
The important result is a real DOM confused-deputy counterexample: an isolated context
can be genuine while the command it forwards was chosen by a hostile page or iframe.
Overall privileged Hybrid remains **NO-GO**. Android worlds are not implemented or qualified.
