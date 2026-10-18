namespace ModernFormsNext.WebView.Sample;

// HTML-parsed page/frame actors are untrusted. Native evaluation only asks these
// already-loaded actors to run; it does not simulate whether their source could load.
internal static class TestContent
{
    internal const string Html = """
        <!doctype html><html><body><p>Unprivileged world research</p>
        <script>
        window.proof=[];
        addEventListener('message',e=>{
          if(!e.data||e.data.kind!=='world-proof') return;
          if(proof.length<16) proof.push(e.data);
          chrome.webview.postMessage('untrusted-relay');
          if(e.data.actor==='same') {
            document.documentElement.dataset.command='iframe-dom';
            document.dispatchEvent(new Event('world-command'));
          }
        });
        window.attackWorld=name=>{
          proof.push({actor:'page',direct:typeof window[name]});
          chrome.webview.postMessage(JSON.stringify({name:name,executionContextId:1,payload:'forged'}));
          for(const frame of document.querySelectorAll('iframe')) frame.contentWindow.postMessage({name:name},'*');
          document.documentElement.dataset.command='page-dom';
          document.dispatchEvent(new Event('world-command'));
        };
        </script>
        <iframe srcdoc="<script>addEventListener('message',e=>parent.postMessage({kind:'world-proof',actor:'same',direct:typeof window[e.data.name],parentDirect:typeof parent[e.data.name]},'*'));</script>"></iframe>
        <iframe sandbox="allow-scripts" srcdoc="<script>addEventListener('message',e=>{let denied=false;try{void parent.document.body}catch{denied=true}parent.postMessage({kind:'world-proof',actor:'opaque',direct:typeof window[e.data.name],parentDomDenied:denied},'*')});</script>"></iframe>
        </body></html>
        """;
}
