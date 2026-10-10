# Experimental WebView baseline

ModernFormsNext issue #60 already provides the native hosting contract. This feature package
consumes it without defining another hosting or platform service system.

- [Architecture and ownership](architecture.md)
- [Public API, event order and examples](public-api.md)
- [Windows adapter](windows.md)
- [Android adapter](android.md)
- [Security and content policy](security.md)
- [Platform capabilities and limitations](capabilities.md)
- [Build, tests and native validation](testing.md)
- [Final review, API inventory and qualification](final-review.md)
- [Historical implementation report](baseline-report.md)

APIs are experimental and unpublished. Build against the pinned framework source using
scripts/bootstrap-framework.ps1 and ModernFormsNextSourceRoot, as described in the root README.
