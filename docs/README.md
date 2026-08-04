# Architecture notes

This directory will contain approved architecture records and usage
documentation for ModernFormsNext.WebView.

The first design task is a platform-neutral native-content hosting contract.
It must cover bounds, clipping, visibility, focus, DPI changes, input routing,
dispatcher affinity, and native resource lifetime without exposing WebView2 or
Android types to shared projects.

No hosting contract or public WebView API is finalized yet.
