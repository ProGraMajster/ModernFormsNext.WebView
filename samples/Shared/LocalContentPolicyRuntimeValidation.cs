using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class LocalContentPolicyRuntimeValidation
{
    internal static async Task RunAsync(WebView browser, Action<string> report)
    {
        RuntimeValidation.Require(browser.BrowserCapabilities.HasFlag(WebViewCapabilities.ConfigurableLocalContent), "typed local policy capability");
        // HTML-parsed child scripts are the attackers. Native ExecuteScript is an
        // explicit application operation, not a valid test of page eval restrictions.
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<p>Frame actor control</p>"));
        await browser.ExecuteScriptAsync("window.policyFrameControls=0;addEventListener('message',e=>{if(e.data==='policy-frame-script-ran')++policyFrameControls;});null");
        await AddSrcdocActorsAsync(browser);
        await WaitAsync(browser, "window.policyFrameControls===2&&window.policyFrameLoads===2");
        var strict = ApplicationContentSample.CreateFixture(WebViewLocalContentPolicy.Default);
        var application = ApplicationContentSample.CreateFixture(WebViewLocalContentPolicy.Application);
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(strict));
        await WaitAsync(browser, "window.policyDone===true");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!policyFetchDenied&&document.baseURI===location.href") == "true", "unchanged default fetch/base denial despite permissive meta CSP");
        await AssertPublicAssetsAsync(browser, true, true, true);
        using var transport = new WebViewTransport(browser); var old = transport.CurrentDocument;

        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(application));
        RuntimeValidation.Require(old.CancellationToken.IsCancellationRequested, "policy replacement cancels canonical old document");
        await WaitAsync(browser, "window.policyDone===true");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("policyData.value===42&&document.baseURI===location.origin+'/'") == "true", "application self fetch/JSON and base URI actually work");
        await AssertPublicAssetsAsync(browser, true, true, true);
        await NegativeRequestsAsync(browser, application);
        await browser.ExecuteScriptAsync("""
            window.policyNegativeDone=false;window.policyExternalScript=false;
            (()=>{const s=document.createElement('script');s.src='https://foreign.invalid/foreign.js';s.onerror=()=>window.policyExternalScriptDenied=true;document.head.append(s);
            const b=document.querySelector('base');b.href='https://foreign.invalid/';
            const f=document.createElement('iframe');f.src='frame.html';document.body.append(f);
            window.policyNegativeDone=true;})();null
            """);
        await AddSrcdocActorsAsync(browser);
        await WaitAsync(browser, "window.policyFrameLoads===2&&!!window.policyExternalScriptDenied&&policyViolations.some(x=>x==='base-uri')&&policyViolations.some(x=>x==='frame-src')&&policyViolations.some(x=>x.startsWith('script-src'))");
        // WebView2 155 falls back to the document URL; Android provider113 retains
        // the previous valid self base. Both must reject the foreign origin. The
        // policy promises an origin restriction, not a particular cached base path.
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!policyEval&&!!policyEvalDenied&&!window.policyInline&&!window.policyFrame&&" +
            "(document.baseURI===location.href||document.baseURI===location.origin+'/')&&new URL(document.baseURI).origin===location.origin") == "true",
            "page eval/inline/frame script denied and foreign base rejected with a same-origin document/base URL");
        string otherPort = JsonSerializer.Serialize(new UriBuilder(application.Origin) { Port = 444 }.Uri.AbsoluteUri);
        await browser.ExecuteScriptAsync("document.querySelector('base').href=" + otherPort + ";null");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("new URL(document.baseURI).origin===location.origin") == "true", "same-host foreign-port base denied");
        // frame-src controls navigated frame resources, not every possible DOM context.
        // The srcdoc cases above test inherited inline-script denial; this is not proof
        // of a complete frame/realm sandbox or original-script identity.
        report("PASS typed application CSP real self JSON/base + default/meta/foreign host/port/base/script/eval/frame denials; no native privilege");

        foreach (string selector in new[] { "scripts", "styles", "images" })
        {
            var policy = selector switch
            {
                "scripts" => new WebViewLocalContentPolicy(scripts: WebViewLocalContentSource.None),
                "styles" => new WebViewLocalContentPolicy(styles: WebViewLocalContentSource.None),
                _ => new WebViewLocalContentPolicy(images: WebViewLocalContentSource.None)
            };
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(ApplicationContentSample.CreateFixture(policy)));
            await AssertPublicAssetsAsync(browser, selector != "scripts", selector != "styles", selector != "images");
        }
        // Independent opt-ins also exercise each selector without the convenience preset.
        foreach (bool connect in new[] { false, true })
        {
            var policy = connect ? new WebViewLocalContentPolicy(connections: WebViewLocalContentSource.Self) :
                new WebViewLocalContentPolicy(baseUri: WebViewLocalContentSource.Self);
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(ApplicationContentSample.CreateFixture(policy)));
            await WaitAsync(browser, "window.policyDone===true");
            string predicate = connect ? "policyData.value===42&&document.baseURI===location.href" :
                "!!policyFetchDenied&&document.baseURI===location.origin+'/'";
            RuntimeValidation.Require(await browser.ExecuteScriptAsync(predicate) == "true", "base/connect opt-ins are independent");
        }
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(application));
        await WaitAsync(browser, "window.policyDone===true");
        browser.Stop();
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "policy reload after Stop"));
        await WaitAsync(browser, "window.policyDone===true");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("policyData.value===42") == "true", "reload retains selected policy");
        var rejected = new TaskCompletionSource<WebViewErrorCategory>(TaskCreationOptions.RunContinuationsAsynchronously);
        var departureDocument = transport.CurrentDocument;
        EventHandler<WebViewNavigationFailedEventArgs> failure = (_, e) => rejected.TrySetResult(e.Category);
        browser.NavigationFailed += failure;
        try
        {
            try { await browser.ExecuteScriptAsync("setTimeout(()=>location.href='https://foreign.invalid/denied',0);null"); }
            catch (OperationCanceledException) when (departureDocument.CancellationToken.IsCancellationRequested)
            {
                // The page's navigation can invalidate its originating EvaluateJavascript
                // before Android returns that callback. Cancellation is correct; still
                // require the typed rejection and unchanged local origin below.
            }
            RuntimeValidation.Require(await rejected.Task.WaitAsync(TimeSpan.FromSeconds(10)) == WebViewErrorCategory.InvalidAddress,
                "application self connections do not permit a foreign page-initiated departure");
            RuntimeValidation.Require(departureDocument.CancellationToken.IsCancellationRequested, "departure invalidates the original document operations");
            RuntimeValidation.Require(browser.Source?.Host == application.Origin.Host, "rejected departure retains local logical source");
        }
        finally { browser.NavigationFailed -= failure; }
        await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "application reload after rejected departure"));
        await AssertApplicationAsync(browser);
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(strict));
        await WaitAsync(browser, "window.policyDone===true");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("!!policyFetchDenied") == "true", "returning to default does not inherit relaxed policy");
        RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "no authentication capability promotion");
        report("PASS native independent script/style/image/connect/base selectors + rejected departure/replacement/Stop/reload/default restoration");
    }

    internal static async Task AssertApplicationAsync(WebView browser)
    {
        await WaitAsync(browser, "window.policyDone===true");
        RuntimeValidation.Require(await browser.ExecuteScriptAsync("policyData.value===42&&document.baseURI===location.origin+'/'") == "true",
            "application policy retained after native host recreation");
    }

    private static Task AddSrcdocActorsAsync(WebView browser)
    {
        string source = JsonSerializer.Serialize("<script>parent.postMessage('policy-frame-script-ran','*');</script>");
        return browser.ExecuteScriptAsync("window.policyFrameLoads=0;(()=>{for(const opaque of [false,true]){" +
            "const f=document.createElement('iframe');if(opaque)f.sandbox='allow-scripts';f.onload=()=>++window.policyFrameLoads;" +
            "f.srcdoc=" + source + ";document.body.append(f);}})();null");
    }

    private static async Task NegativeRequestsAsync(WebView browser, WebViewLocalContent content)
    {
        string foreignPort = new UriBuilder(content.Origin) { Port = 444 }.Uri.AbsoluteUri;
        await browser.ExecuteScriptAsync("""
            window.policyRequestsDone=false;window.policyRequestResults={};
            (async()=>{
              const results=policyRequestResults;
              results.missing=(await fetch('unlisted.json')).status;
              results.method=(await fetch('data.json',{method:'POST'})).status;
              results.path=(await fetch('bad%2fpath.json')).status;
              results.csp=(await fetch('data.json')).headers.get('Content-Security-Policy');
              results.foreign=false;results.port=false;
              try{await fetch('https://foreign.invalid/data.json');results.foreign=true;}catch{}
              try{await fetch(PORT_URI);results.port=true;}catch{}
              window.policyRequestsDone=true;
            })();null
            """.Replace("PORT_URI", JsonSerializer.Serialize(foreignPort + "data.json"), StringComparison.Ordinal));
        await WaitAsync(browser, "window.policyRequestsDone===true");
        using var result = JsonDocument.Parse(await browser.ExecuteScriptAsync("window.policyRequestResults"));
        var value = result.RootElement;
        RuntimeValidation.Require(value.GetProperty("missing").GetInt32() == 404 && value.GetProperty("method").GetInt32() == 405 && value.GetProperty("path").GetInt32() == 400, "unlisted/method/escaped path denied through native resolver");
        RuntimeValidation.Require(!value.GetProperty("foreign").GetBoolean() && !value.GetProperty("port").GetBoolean(), "foreign host/port denied");
        RuntimeValidation.Require(value.GetProperty("csp").GetString() == content.Policy.ContentSecurityPolicy, "native response has exact selected CSP");
        await WaitAsync(browser, "policyViolations.some(x=>x==='connect-src')");
    }

    private static async Task AssertPublicAssetsAsync(WebView browser, bool script, bool style, bool image)
    {
        using var result = JsonDocument.Parse(await browser.ExecuteScriptAsync("({script:!!window.policyScript,style:getComputedStyle(document.getElementById('policy-title')).color,image:document.getElementById('policy-image').naturalWidth})"));
        var value = result.RootElement;
        RuntimeValidation.Require(value.GetProperty("script").GetBoolean() == script, "script selector native effect");
        RuntimeValidation.Require((value.GetProperty("style").GetString() == "rgb(20, 70, 120)") == style, "style selector native effect");
        RuntimeValidation.Require((value.GetProperty("image").GetInt32() == 1) == image, "image selector native effect");
    }
    private static async Task WaitAsync(WebView browser, string predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await browser.ExecuteScriptAsync(predicate, deadline.Token) != "true") await Task.Delay(20, deadline.Token);
    }
}
