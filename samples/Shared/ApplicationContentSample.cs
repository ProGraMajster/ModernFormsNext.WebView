using System.Text;

namespace ModernFormsNext.WebView.Sample;

// This opt-in changes only content loading. It does not declare a verified trusted realm
// or export native handlers. Every byte here is intentionally public to page JavaScript.
internal static class ApplicationContentSample
{
    internal static WebViewLocalContent Create(WebViewLocalContentPolicy? policy = null) => CreateCore(policy ?? WebViewLocalContentPolicy.Application, false);
    internal static WebViewLocalContent CreateFixture(WebViewLocalContentPolicy policy) => CreateCore(policy, true);
    private static WebViewLocalContent CreateCore(WebViewLocalContentPolicy policy, bool attacks) => new(new Dictionary<string, byte[]>
    {
        ["index.html"] = Encoding.UTF8.GetBytes("""
            <!doctype html><html><head>
            ATTACK_META
            <base href="/"><link rel="stylesheet" href="app.css"><script src="app.js" defer></script>
            ATTACK_INLINE</head><body>
            <h1 id="policy-title">Explicit application resource policy</h1>
            <p>Same-origin JSON fetch and base URI are opt-in. No C# handlers or authenticated messages.</p>
            <img id="policy-image" src="pixel.png"><pre id="policy-result">Waiting for public JSON</pre>
            ATTACK_FRAME</body></html>
            """.Replace("ATTACK_META", attacks ? "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src * 'unsafe-inline' 'unsafe-eval'; script-src * 'unsafe-inline' 'unsafe-eval'; style-src * 'unsafe-inline'; connect-src *; base-uri *; frame-src *\">" : "", StringComparison.Ordinal)
                .Replace("ATTACK_INLINE", attacks ? "<script>window.policyInline=true;</script>" : "", StringComparison.Ordinal)
                .Replace("ATTACK_FRAME", attacks ? "<iframe id='policy-frame' src='frame.html'></iframe>" : "", StringComparison.Ordinal)),
        ["app.js"] = Encoding.UTF8.GetBytes("""
            'use strict';
            window.policyViolations=[];
            addEventListener('securitypolicyviolation',e=>{if(policyViolations.length<64)policyViolations.push(e.violatedDirective);});
            addEventListener('message',e=>{if(e.data==='policy-frame-script-ran')window.policyFrame=true;});
            window.policyScript=true;
            ATTACK_EVAL
            window.policyDone=false;
            fetch('data.json').then(async r=>{window.policyData=await r.json();document.getElementById('policy-result').textContent=JSON.stringify(policyData);},
              ()=>{window.policyFetchDenied=true;document.getElementById('policy-result').textContent='Fetch denied by selected policy';})
              .finally(()=>window.policyDone=true);
            """.Replace("ATTACK_EVAL", attacks ? "window.policyEval=false;try{eval('window.policyEval=true');}catch{window.policyEvalDenied=true;}" : "", StringComparison.Ordinal)),
        ["app.css"] = "#policy-title{color:rgb(20,70,120)}"u8.ToArray(),
        ["data.json"] = "{\"value\":42}"u8.ToArray(),
        ["pixel.png"] = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLttAAAAABJRU5ErkJggg=="),
        ["frame.html"] = "<script src='frame.js'></script>"u8.ToArray(),
        ["frame.js"] = "parent.policyFrame=true;"u8.ToArray()
    }, policy);
}
