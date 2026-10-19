# Experimental WebView baseline

- [Next-phase delivery, merge audit and recorded development snapshots](next-phase-delivery-report.md)

- [Security-policy/Blazor/world delivery history and subsequent audit](security-policy-development-report.md)
- [Independent baseline preview/stable readiness and official package probe](baseline-preview-readiness.md)

ModernFormsNext issue #60 already provides the native hosting contract. This feature package
consumes it without defining another hosting or platform service system.

- [Architecture and ownership](architecture.md)
- [Public API, event order and examples](public-api.md)
- [Windows adapter](windows.md)
- [Android adapter](android.md)
- [Security and content policy](security.md)
- [Local public resources and platform decision](local-content.md)
- [Explicit typed local policy API, effects/risks/support and examples](local-content-policy.md)
- [Per-view navigation schemes, capabilities and security-control inventory](navigation-scheme-filter.md)
- [Hybrid trust matrix and prerequisites](hybrid-security.md)
- [Hybrid whole-realm trust strategy, platform/world alternatives and acceptance gates](hybrid-trust-strategy.md)
- [Baseline messaging native verification (#33)](issue33-security-validation.md)
- [Reusable transport (#11)](issue11-transport.md)
- [Versioned unprivileged RPC (#12)](issue12-rpc.md)
- [PR #36–#38 final audit and sequential merges](pr36-38-final-audit.md)
- [AndroidX origin/frame prototype and ADR (#16)](issue16-android-origin-research.md)
- [Windows message hardening and privileged NO-GO (#15)](issue15-windows-message-hardening.md)
- [Current Phase 2 follow-up report and next issue order](phase2-followup-report.md)
- [Final #39/#40 audit and normal merges](pr39-40-final-audit.md)
- [Final #41/#42/#43 audit and normal merges](pr41-43-final-audit.md)
- [Conversion deadlines and cancellation (#19)](issue19-errors-cancellation.md)
- [Executable .NET 10 Razor/Blazor feasibility and NO-GO ADR (#22)](issue22-blazor-feasibility.md)
- [Explicit application-policy Blazor assets vs authorized IPC experiment](blazor-application-policy.md)
- [Bounded Windows execution-world diagnostic experiment](windows-world-experiment.md)
- [Current four-stage Phase 2 maturity report](phase2-maturity-report.md)
- [PR #35 final audit and merge](pr35-final-audit.md)
- [Phase 2 progress, dependency and release gates](phase2-progress.md)
- [Platform capabilities and limitations](capabilities.md)
- [Build, tests and native validation](testing.md)
- [Final review, API inventory and qualification](final-review.md)
- [Historical implementation report](baseline-report.md)

APIs are experimental and unpublished. Build against the pinned framework source using
scripts/bootstrap-framework.ps1 and ModernFormsNextSourceRoot, as described in the root README.
