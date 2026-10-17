# AndroidX origin/frame experiment (#16)

This is a standalone **research/test APK**, outside the product solution. Its Activity owns
one SDK WebView; it is not an alternative ModernFormsNext host, transport or dispatcher.
Production providers and the baseline sample do not reference AndroidX. The APK installs
under its own application ID and is never a package/release deliverable.

Pinned Microsoft binding `Xamarin.AndroidX.WebKit` 1.15.0.2 includes native WebKit 1.15.0,
targets .NET10/Android36 and declares minSdk23. Inspected 1.16.0.1/1.17.0 AARs require24;
they cannot replace this dependency while preserving API23. The research reference is the
only consumer of the central version. Inspect resolved transitive dependencies and the
merged manifest as well as the primary AAR before changing it.

```powershell
$framework = & ./scripts/bootstrap-framework.ps1
dotnet build ./research/AndroidOriginProbe -c Debug -t:SignAndroidPackage -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:ModernFormsNextSourceRoot=$framework -p:EnableWindowsTargeting=true
./research/AndroidOriginProbe/Validate.ps1 -AdbPath <absolute-adb-path> -Serial emulator-5554 -Configuration Debug -Recreate
# Repeat with Release. CI separately builds/signs this APK; no emulator pass is inferred from CI.
```

Java SDK21 must be selected by the environment or `-p:JavaSdkDirectory=...`. The APK currently
targets android-x64 for the selected emulator. Validation touches only this explicit serial
and application, captures PID-filtered logs and never clears global logs or grants permissions.
Two completed probes and one disposed old fixture are required for `-Recreate`.

The interceptor serves fixed public adversarial HTML at two private test HTTPS origins and
a mismatched port. These fixtures intentionally exercise frames/inline script; production
WebViewLocalContent CSP is untouched. No external network, file mapping, cleartext override,
TLS bypass, wildcard origin rule, AddJavascriptInterface or privileged C# callback exists here.
The sole response is a fixed public acknowledgement; RPC coverage is the existing JS echo.

The terminal marker means **the experiment executed**, not that authentication is secure.
See [ADR/decision and qualification](../../docs/issue16-android-origin-research.md).
