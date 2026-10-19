using global::Android.App;
using global::Android.Content.PM;
using ModernFormsNext.WindowKit.Backend.Android.Windowing;

namespace ModernFormsNext.WebView.Sample;

[Activity(Name = "com.programajster.modernformsnext.webview.sample.MainActivity", MainLauncher = true,
    Exported = true, WindowSoftInputMode = global::Android.Views.SoftInput.AdjustResize)]
public sealed class MainActivity : AndroidWindowActivity
{
    protected override void OnStartApplication()
    {
        var form = new SampleForm();
        bool floor = Intent?.GetBooleanExtra("validate-floor", false) == true;
        bool popup = Intent?.GetBooleanExtra("validate-popup", false) == true;
        bool lifecycle = Intent?.GetBooleanExtra("validate-lifecycle", false) == true;
        bool nativeBack = Intent?.GetBooleanExtra("validate-back", false) == true;
        string? runId = Intent?.GetStringExtra("validation-run-id");
        string? tlsProbe = Intent?.GetStringExtra("validation-tls-uri");
        bool validate = popup || floor || lifecycle || Intent?.GetBooleanExtra("validate", false) == true;
        if (validate)
            form.Shown += async (_, _) =>
            {
                try
                {
                    global::Android.Util.Log.Info("MFNWebView", "RUN START " + runId);
                    if (popup)
                    {
                        await NewWindowRuntimeValidation.RunAsync(form, m => global::Android.Util.Log.Info("MFNWebView", m), () =>
                        { global::Android.Util.Log.Info("MFNWebView", "POPUP GESTURE WAIT"); return Task.CompletedTask; });
                        global::Android.Util.Log.Info("MFNWebView", "POPUP PASS"); return;
                    }
                    if (floor)
                    {
                        await BaselineFloorRuntimeValidation.RunAsync(form, m => global::Android.Util.Log.Info("MFNWebView", m));
                        if (nativeBack) await BaselineFloorRuntimeValidation.NativeBackAsync(form, m => global::Android.Util.Log.Info("MFNWebView", m));
                        if (lifecycle) await AndroidLifecycleValidation.RunAsync(form, false, m => global::Android.Util.Log.Info("MFNWebView", m));
                        global::Android.Util.Log.Info("MFNWebView", "FLOOR PASS (separate from full modern-provider suite)");
                        return;
                    }
                    await RuntimeValidation.RunAsync(form, m => global::Android.Util.Log.Info("MFNWebView", m));
                    await DefaultSecurityRuntimeValidation.RunAsync(form.Browser, m => global::Android.Util.Log.Info("MFNWebView", m));
                    if (tlsProbe is not null)
                        await DefaultSecurityRuntimeValidation.TlsAsync(form.Browser, new Uri(tlsProbe), m => global::Android.Util.Log.Info("MFNWebView", m));
                    await MessageSecurityRuntimeValidation.RunRedirectAsync(form.Browser, new Uri("https://example.com/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    await NavigationRuntimeValidation.RunAsync(form.Browser, new Uri("https://example.com/"), new Uri("https://example.org/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    await NavigationRuntimeValidation.RunPostAsync(form.Browser, new Uri("https://example.com/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    await NavigationSchemeRuntimeValidation.RunPostAsync(form.Browser, new Uri("https://example.com/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    // Leave the static local document visible for rotation/recreation checks.
                    await RuntimeValidation.NavigateAsync(form.Browser, () => form.Browser.NavigateToLocalContent(LocalContentSample.Create()));
                    if (lifecycle) await AndroidLifecycleValidation.RunAsync(form, true, m => global::Android.Util.Log.Info("MFNWebView", m));
                    global::Android.Util.Log.Info("MFNWebView", "RUNTIME PASS");
                }
                catch (Exception error)
                {
                    if (floor && form.Browser.IsInitialized && form.Browser.IsJavaScriptEnabled)
                    {
                        try
                        {
                            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                            string snapshot = await form.Browser.ExecuteScriptAsync("JSON.stringify({ready:document.readyState,frames:document.getElementsByTagName('iframe').length,actors:window.floorFrames,local:window.floorLocal,body:document.body?document.body.innerHTML.slice(0,600):null})", deadline.Token);
                            global::Android.Util.Log.Info("MFNWebView", "FLOOR failure snapshot " + snapshot);
                        }
                        catch (Exception snapshotError) { global::Android.Util.Log.Info("MFNWebView", "FLOOR snapshot unavailable " + snapshotError.GetType().Name); }
                    }
                    global::Android.Util.Log.Error("MFNWebView", "RUNTIME FAIL: " + error);
                }
            };
        if (validate)
        {
            form.Browser.InitializationStateChanged += (_, _) => global::Android.Util.Log.Info("MFNWebView",
                $"LIFECYCLE {form.Browser.InitializationState} generation={form.Browser.GetDiagnostics().PeerGeneration}");
            form.Browser.NavigationCompleted += (_, _) => global::Android.Util.Log.Info("MFNWebView", "LIFECYCLE replay/completion");
        }
        ModernFormsNext.Application.Run(form);
    }

    protected override void OnCreate(global::Android.OS.Bundle? state)
    {
        base.OnCreate(state);
        if (IsValidation)
        {
            // Test-window scope only: no keyguard dismissal, wake lock, settings or
            // global timeout changes. The human still has to unlock the test phone.
            Window?.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
            global::Android.Util.Log.Info("MFNWebView", "Activity created");
        }
    }
    protected override void OnDestroy()
    { try { base.OnDestroy(); } finally { if (IsValidation) global::Android.Util.Log.Info("MFNWebView", "Activity destroyed"); } }

    private bool IsValidation => Intent?.GetBooleanExtra("validate", false) == true ||
        Intent?.GetBooleanExtra("validate-popup", false) == true ||
        Intent?.GetBooleanExtra("validate-floor", false) == true || Intent?.GetBooleanExtra("validate-lifecycle", false) == true;
}
