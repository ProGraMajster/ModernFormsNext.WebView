using global::Android.App;
using global::Android.Runtime;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WebView.Android;

namespace ModernFormsNext.WebView.Sample;

[Application]
public sealed class SampleApplication(IntPtr handle, JniHandleOwnership ownership) : global::Android.App.Application(handle, ownership)
{
    public override void OnCreate()
    {
        base.OnCreate();
        AndroidWindowKit.Initialize(new AndroidWindowKitOptions(this));
        AndroidWebViewProvider.Register();
    }
}
