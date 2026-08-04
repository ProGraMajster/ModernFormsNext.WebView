# Roadmap

ModernFormsNext.WebView is in the **Planning** phase. Roadmap items are ordered
dependencies, not release commitments. No implementation stage is complete.

- [ ] **1. Planning and architecture** *(current)*

  Define scope, platform boundaries, lifecycle, threading, security, and
  compatibility expectations.
- [ ] **2. Native content hosting contract**

  Design a platform-neutral contract for positioning, clipping, focus, DPI,
  visibility, lifetime, and input of native child content.
- [ ] **3. Shared WebView API**

  Design the public control API, navigation model, events, scripting, and
  JavaScript-to-C# messaging without leaking platform types.
- [ ] **4. Windows WebView2 backend**

  Implement the Windows backend after the hosting contract and shared API are
  approved.
- [ ] **5. Android WebView backend**

  Implement the Android backend using the native `android.webkit.WebView`.
- [ ] **6. Samples and tests**

  Add focused Windows and Android samples plus automated contract tests and
  documented manual validation.
- [ ] **7. Preview NuGet release**

  Publish an explicitly prerelease package only after implementation and
  validation criteria are met.
- [ ] **8. Stable release**

  Stabilize compatibility, documentation, diagnostics, and supported-platform
  guarantees before the first stable release.

Implementation work remains blocked until the main ModernFormsNext framework
has stabilized and the relevant architecture has been approved.
