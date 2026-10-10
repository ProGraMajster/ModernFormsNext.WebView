# Contributing

The extension is experimental. Begin with an audit of the current framework contract
and relevant roadmap issue. The local baseline is explicitly authorized; older issue
text that describes a planning-only block is historical. Releases remain separate work.

Keep shared API free of WebView2/Android types. Implement supported public
INativeViewFactory/INativeViewPeer seams and consume typed sites. Never implement the
framework-owned provider/session/callback contracts, copy native hosting algorithms,
use reflection or friend access to framework internals, or edit main for convenience.
Report a genuine public hosting-contract blocker before attempting a workaround.

Document public/protected APIs and behavior changes. Keep native resource disposal,
callback identity, thread affinity and security policy reviewable. Use the existing
WebView samples rather than main ControlGallery or main DemoApp. Do not vendor the
framework or check in absolute machine paths, profiles, APK/AABs, caches, logs or TRX files.

Use the pinned source bootstrap and sequential commands in [testing](docs/testing.md).
Run Debug/Release builds, all applicable tests, native validation when available,
and `git diff --check`. Report actual automated/native/manual results separately.
NuGet publication, commits, pushes, PRs, issue closure and version selection require
their own authorization.
