# ModernFormsNext.WebView

> **Project status: Planning**

ModernFormsNext.WebView is a planned official extension for
[ModernFormsNext](https://github.com/ProGraMajster/ModernFormsNext). It is not
ready for use and does not yet contain a WebView implementation.

## Goal

The project will provide a cross-platform WebView control for ModernFormsNext.
The first target platforms are Windows and Android:

- Windows will use Microsoft Edge WebView2.
- Android will use the native `android.webkit.WebView`.

The extension will remain separate from the main ModernFormsNext package. This
keeps heavyweight platform dependencies outside the core framework and lets
applications opt in only when embedded web content is required.

Implementation will begin only after the main ModernFormsNext framework has
stabilized. The current repository is reserved for planning, architecture,
documentation, and buildable project skeletons.

## Planned capabilities

- Navigate to a URL.
- Display supplied HTML content.
- Support `GoBack`, `GoForward`, `Reload`, and `Stop`.
- Execute JavaScript.
- Exchange messages between JavaScript and C#.
- Expose navigation lifecycle and failure events.
- Integrate focus, DPI scaling, clipping, and native-content hosting with
  ModernFormsNext.

## Repository boundaries

- No WebView2 or Android WebView integration is implemented yet.
- No WebView2 package or other platform runtime dependency is referenced.
- The projects do not reference a local ModernFormsNext checkout.
- No NuGet package or GitHub Release is produced at this stage.
- No public WebView API has been committed yet.

See [ROADMAP.md](ROADMAP.md) for the staged plan and
[CONTRIBUTING.md](CONTRIBUTING.md) before proposing changes.

## License

ModernFormsNext.WebView is licensed under the MIT License, matching the main
ModernFormsNext repository. See [LICENSE](LICENSE).
