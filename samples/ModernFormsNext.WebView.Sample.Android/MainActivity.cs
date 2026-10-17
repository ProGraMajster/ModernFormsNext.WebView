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
        if (Intent?.GetBooleanExtra("validate", false) == true)
            form.Shown += async (_, _) =>
            {
                try
                {
                    await RuntimeValidation.RunAsync(form, m => global::Android.Util.Log.Info("MFNWebView", m));
                    await MessageSecurityRuntimeValidation.RunRedirectAsync(form.Browser, new Uri("https://example.com/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    await NavigationRuntimeValidation.RunAsync(form.Browser, new Uri("https://example.com/"), new Uri("https://example.org/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    await NavigationRuntimeValidation.RunPostAsync(form.Browser, new Uri("https://example.com/"),
                        m => global::Android.Util.Log.Info("MFNWebView", m));
                    // Leave the static local document visible for rotation/recreation checks.
                    await RuntimeValidation.NavigateAsync(form.Browser, () => form.Browser.NavigateToLocalContent(LocalContentSample.Create()));
                    global::Android.Util.Log.Info("MFNWebView", "RUNTIME PASS");
                }
                catch (Exception error) { global::Android.Util.Log.Error("MFNWebView", "RUNTIME FAIL: " + error); }
            };
        if (Intent?.GetBooleanExtra("validate", false) == true)
        {
            form.Browser.InitializationStateChanged += (_, _) => global::Android.Util.Log.Info("MFNWebView",
                $"LIFECYCLE {form.Browser.InitializationState} generation={form.Browser.GetDiagnostics().PeerGeneration}");
            form.Browser.NavigationCompleted += (_, _) => global::Android.Util.Log.Info("MFNWebView", "LIFECYCLE replay/completion");
        }
        ModernFormsNext.Application.Run(form);
    }

    protected override void OnCreate(global::Android.OS.Bundle? state)
    { base.OnCreate(state); if (Intent?.GetBooleanExtra("validate", false) == true) global::Android.Util.Log.Info("MFNWebView", "Activity created"); }
    protected override void OnDestroy()
    { try { base.OnDestroy(); } finally { if (Intent?.GetBooleanExtra("validate", false) == true) global::Android.Util.Log.Info("MFNWebView", "Activity destroyed"); } }
}
