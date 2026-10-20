using System.Net;

namespace ModernFormsNext.WebView.Sample;

// Parsed hostile programs, deliberately no CSP. These are NOT the authorized world
// program. Their messages only record attempts; no page data creates native permits.
internal static class TestContent
{
    private const string Attack = """
        function attack(actor,c){
          var p={actor:actor,direct:typeof window[c.name],steps:0};
          try{if(typeof window[c.name]==='function')window[c.name](c.payload);}catch(e){}p.steps++;
          try{p.parentDirect=typeof parent[c.name];if(typeof parent[c.name]==='function')parent[c.name](c.payload);}catch(e){p.parentDenied=true;}p.steps++;
          try{parent.closedRelay(c.payload);}catch(e){}p.steps++;
          document.documentElement.dataset.command=c.payload;document.dispatchEvent(new Event('closed-counter-command'));p.steps++;
          parent.postMessage({kind:'closed-relay',payload:c.payload},'*');p.steps++;
          Object.prototype.closedCounterPayload=c.payload;Object.prototype[c.name]=function(){parent.postMessage({kind:'closed-relay',payload:c.payload},'*');};p.steps++;
          var f=document.createElement('form');f.name=c.name;document.body.appendChild(f);p.steps++;
          var forged={name:c.name,executionContextId:c.contextId,payload:c.payload};
          try{chrome.webview.postMessage(JSON.stringify(forged));}catch(e){}
          parent.postMessage({kind:'closed-forged',packet:forged},'*');p.steps++;
          parent.postMessage({kind:'closed-proof',proof:p},'*');
        }
        """;
    private static string Frame(string actor) => "<script>" + Attack +
        "addEventListener('message',e=>{if(e.data&&e.data.kind==='run-closed-actor')attack('" + actor + "',e.data);});" +
        "parent.postMessage({kind:'closed-ready',actor:'" + actor + "'},'*');</script>";
    internal static string Html => """
        <!doctype html><html><body><p>Closed program hostile fixture</p><script>
        window.closedProof=[];window.closedReady=[];
        window.closedRelay=function(payload){document.documentElement.dataset.command=payload;document.dispatchEvent(new Event('closed-counter-command'));};
        addEventListener('message',e=>{
          if(!e.data)return;
          if(e.data.kind==='closed-ready'&&closedReady.length<4)closedReady.push(e.data.actor);
          if(e.data.kind==='closed-proof'&&closedProof.length<8){closedProof.push(e.data.proof);chrome.webview.postMessage('actor-proof:'+e.data.proof.actor);}
          if(e.data.kind==='closed-relay')closedRelay(e.data.payload);
          if(e.data.kind==='closed-forged')chrome.webview.postMessage(JSON.stringify(e.data.packet));
        });
        window.runClosedActor=function(actor,c){if(actor==='main')attack('main',c);else document.getElementById(actor).contentWindow.postMessage(Object.assign({kind:'run-closed-actor'},c),'*');};
        """ + Attack + "</script><iframe id='same' srcdoc=\"" + WebUtility.HtmlEncode(Frame("same")) +
        "\"></iframe><iframe id='opaque' sandbox='allow-scripts' srcdoc=\"" + WebUtility.HtmlEncode(Frame("opaque")) + "\"></iframe></body></html>";
}
