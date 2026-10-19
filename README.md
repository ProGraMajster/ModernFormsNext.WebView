# ModernFormsNext.WebView

> **Development / Experimental — no preview or stable release**

An opt-in browser extension in a separate repository from
[ModernFormsNext](https://github.com/ProGraMajster/ModernFormsNext). `WebView : NativeViewHost`
uses the main framework's implemented native hosting contract (#60). It adds no
browser dependency to the framework and implements no second layout/focus/Activity system.

Windows uses official Microsoft WebView2 Core APIs. Android uses platform
`android.webkit.WebView`, with API 23 as its minimum. The experimental implementation includes
URI/raw HTML navigation, back/forward/reload/stop, neutral events, cancellation,
explicit JSON-result scripting, string messaging, user agent settings and diagnostics.
JavaScript is disabled by default. Cookies/headers APIs, permission UX, downloads,
new-window management and DevTools remain future work; permissions and new windows
are denied and downloads are not saved automatically.

Phase 2 adds [experimental local resources](docs/local-content.md): explicitly public,
immutable HTML/CSS/JS/images at an isolated HTTPS origin, restrictive default CSP and native request
interception on Windows/Android. No directory mapping or HTTP server is exposed.
This does not enable privileged Hybrid messaging. See [progress and gates](docs/phase2-progress.md)
and the [trust matrix](docs/hybrid-security.md).

The [explicit local-content policy](docs/local-content-policy.md) adds typed per-catalog
None/Self restrictions. The original constructor/default policy is unchanged. The
Application opt-in permits same-origin connections/base URI for reviewed public bytes;
it enables no C# handlers or authenticated messaging. Both samples expose a **Local fetch
opt-in** example. Broader platform controls remain separate future work.

The isolated [Blazor application-policy experiment](docs/blazor-application-policy.md)
tests original .NET10 asset startup separately from authorized IPC. No interactive
Blazor host or privileged handler is enabled.

The historical source bootstrap baseline is pinned to framework commit
`69c5ea2b20b07d9df801340c8ae9e3e1ca5dfbc3`. No published package is selected as the
NativeViewHost dependency. See [testing](docs/testing.md) for reproducible build commands.

New development selects the frozen CurrentMaster entry in eng/framework-snapshots.json
(7cfa44e9a992c30d5b1cf5fa76a147eeb2539f56) in a separate source checkout. Earlier
merge audits used 1fc6d8a9d2f9dd31f1279830cdbe60e9eec557b1 and retain that identity. NativeViewHost
is planned for official ModernFormsNext 1.12.0; package alignment is deferred until its
actual publication. Missing hosting API in 1.11.1 is an expected historical control.

The [independent preview/stable checklist](docs/baseline-preview-readiness.md) records
official package compatibility and remaining native/manual/release gates. Optional
Hybrid and Blazor are separate from the ordinary WebView release path. The current
[development report](docs/security-policy-development-report.md) links reviewed merges
and the original implementation/research delivery snapshots.
The [next-phase report](docs/next-phase-delivery-report.md) records accepted merges,
exact source lanes and subsequent development qualification.
The existing application-owned provider is registered explicitly:

```csharp
// Windows startup, STA thread:
WindowsPlatformBootstrap.Initialize();
WindowsWebViewProvider.Register();
var form = new Form();
form.Controls.Add(new WebView { Dock = DockStyle.Fill, Source = new Uri("https://example.com/") });
Application.Run(form);
```

The two samples share the same MFN Form and deterministic offline HTML. Use their
Offline HTML button without network access. Samples explicitly enable JavaScript
for their scripting/messaging demonstration. They are demonstration applications,
not general purpose browsers.

Read [public API](docs/public-api.md), [architecture](docs/architecture.md),
[capabilities](docs/capabilities.md), [security](docs/security.md),
[Windows](docs/windows.md), [Android](docs/android.md) and [testing](docs/testing.md).
[Final review](docs/final-review.md) includes the public API inventory and qualification limits.
[ROADMAP.md](ROADMAP.md) distinguishes local implementation from approved/released work.
This repository uses the MIT [LICENSE](LICENSE).
