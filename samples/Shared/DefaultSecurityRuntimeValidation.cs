using System.Text;

namespace ModernFormsNext.WebView.Sample;

// Native negative controls, kept separate from proposed configuration opt-ins.
// Page actors positively announce completion; absence of execution is never a pass.
internal static class DefaultSecurityRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        var content = new WebViewLocalContent(new Dictionary<string, byte[]>
        {
            ["index.html"] = Encoding.UTF8.GetBytes("<p id='actor'>Default security actor</p><script src='actor.js'></script>"),
            ["actor.js"] = Encoding.UTF8.GetBytes("""
                window.defaultActorStarted=true;
                setTimeout(function(){
                  var popup=window.open('about:blank','mfn-default-denial');
                  window.defaultPopupDenied=!popup;
                  if(popup)popup.close();
                  window.defaultPopupDone=true;
                },0);
                window.defaultGeo='unsupported';window.defaultMedia='unsupported';
                if(navigator.geolocation){
                  window.defaultGeo='pending';
                  navigator.geolocation.getCurrentPosition(function(){window.defaultGeo='granted';},
                    function(e){window.defaultGeo=e.code===1?'denied':'error-'+e.code;},{timeout:5000});
                }
                if(navigator.mediaDevices&&navigator.mediaDevices.getUserMedia){
                  window.defaultMedia='pending';
                  navigator.mediaDevices.getUserMedia({audio:true,video:true}).then(function(stream){
                    stream.getTracks().forEach(function(t){t.stop();});window.defaultMedia='granted';
                  },function(e){window.defaultMedia=e.name;});
                }
                """)
        });
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        await BaselineFloorRuntimeValidation.WaitAsync(browser, "window.defaultActorStarted&&window.defaultPopupDone&&defaultGeo!=='pending'&&defaultMedia!=='pending'");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("defaultPopupDenied===true") == "true", "default page timer popup denied");
        string geo = await browser.ExecuteScriptAsync("defaultGeo");
        string media = await browser.ExecuteScriptAsync("defaultMedia");
        RuntimeValidation.Require(geo is "\"denied\"" or "\"unsupported\"", "default geolocation cannot grant permission");
        RuntimeValidation.Require(media is "\"NotAllowedError\"" or "\"PermissionDeniedError\"" or "\"unsupported\"", "default microphone/camera cannot grant permission");
        RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "default negatives do not promote trust");
        report("PASS native default timer popup denial; geolocation=" + geo + "; camera/microphone=" + media +
            "; unsupported means unavailable API, not a passed permission callback");
    }

    internal static async Task TlsAsync(WebView browser, Uri address, Action<string> report)
    {
        RuntimeValidation.Require(address.Scheme == "https" && address.Host == "127.0.0.1" && address.Port > 0, "owned loopback TLS fixture only");
        var failed = new TaskCompletionSource<WebViewErrorCategory>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationFailedEventArgs> handler = (_, e) => failed.TrySetResult(e.Category);
        browser.NavigationFailed += handler;
        try
        {
            browser.Navigate(address);
            var category = await failed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            report("TLS observed native failure category=" + category);
            RuntimeValidation.Require(category == WebViewErrorCategory.Certificate, "real untrusted TLS certificate is rejected");
        }
        finally { browser.NavigationFailed -= handler; }
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p id='tls-recovery'>TLS rejection recovery</p>"));
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!document.getElementById('tls-recovery')") == "true", "native recovery after TLS denial");
        report("PASS native untrusted loopback TLS certificate rejected + fresh document recovery; no certificate override or installed root");
    }
}
