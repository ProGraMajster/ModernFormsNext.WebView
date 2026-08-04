# Contributing

Thank you for your interest in ModernFormsNext.WebView. The repository is in
the Planning phase, so contributions should currently focus on requirements,
architecture, documentation, risks, and testable contracts.

## Before contributing

1. Search existing issues and roadmap items before opening a proposal.
2. Open an issue before making architectural or public API changes.
3. Keep shared contracts platform-neutral; do not expose WebView2 or Android
   types from shared projects.
4. Do not add WebView2, Android WebView wrappers, or other heavyweight runtime
   dependencies until the corresponding roadmap stage is unblocked.
5. Do not reference a local ModernFormsNext checkout or copy framework code.
6. Document public and protected APIs with complete XML documentation once API
   implementation begins.

## Local validation

Use the SDK selected by `global.json` and run:

```powershell
dotnet restore .\ModernFormsNext.WebView.slnx
dotnet build .\ModernFormsNext.WebView.slnx --configuration Release --no-restore
dotnet format .\ModernFormsNext.WebView.slnx --verify-no-changes --no-restore
```

Tests will be added with the implementation. Pull requests must not publish
NuGet packages, create releases, change product versions, or contain secrets.
