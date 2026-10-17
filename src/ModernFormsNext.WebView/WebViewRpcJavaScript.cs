namespace ModernFormsNext.WebView;

public static partial class WebViewRpcProtocol
{
    /// <summary>Explicit per-document JavaScript endpoint installation, returning true if available.</summary>
    /// <remarks>
    /// Execute with the borrowed transport after document completion, or include in an allowed
    /// application script. Requires Promise, Map, AbortController, TextEncoder and the baseline
    /// modernFormsWebView helper. Exposes only JavaScript register/unregister, never CLR access.
    /// Install again after navigation/reload/recreation. This script is not origin authentication.
    /// Handlers receive (payload, AbortSignal) and should observe advisory cancellation.
    /// </remarks>
    public const string JavaScript = """
        (() => {
          'use strict';
          if (window.modernFormsRpc) return window.modernFormsRpc.version === 1;
          if (typeof AbortController !== 'function' || typeof Map !== 'function' || typeof Set !== 'function' ||
              typeof Promise !== 'function' || typeof TextEncoder !== 'function' ||
              !window.modernFormsWebView || typeof modernFormsWebView.postMessage !== 'function') return false;
          const handlers = new Map(), pending = new Map(), recent = new Set(), encoder = new TextEncoder();
          const maxBytes = 65536, maxPending = 64, maxDepth = 32;
          let retired = false;
          const methodOK = s => typeof s === 'string' && /^[A-Za-z0-9_.-]{1,128}$/.test(s);
          const idOK = s => typeof s === 'string' && /^[0-9a-f]{32}$/.test(s);
          const documentOK = s => typeof s === 'string' && /^[1-9][0-9]{0,18}:[1-9][0-9]{0,18}$/.test(s);
          const sizeOK = s => s.length <= maxBytes && encoder.encode(s).length <= maxBytes;
          const depthOK = (v, depth = 1) => {
            if (v === null || typeof v !== 'object') return true;
            return depth <= maxDepth && Object.keys(v).every(k => depthOK(v[k], depth + 1));
          };
          const fields = (v, names) => v && !Array.isArray(v) && typeof v === 'object' &&
            Object.keys(v).length === names.length && names.every(n => Object.prototype.hasOwnProperty.call(v, n));
          // JSON.parse discards duplicate keys. Scan the already parsed, size-bounded JSON
          // text for top-level property names (including escaped names) before accepting it.
          const uniqueEnvelopeKeys = text => {
            let depth = 0; const seen = new Set();
            for (let i = 0; i < text.length; ++i) {
              const c = text[i];
              if (c === '"') {
                const start = i++;
                while (i < text.length) { if (text[i] === '\\') i += 2; else if (text[i++] === '"') break; }
                const end = i; while (/\s/.test(text[i] || '') && i < text.length) ++i;
                if (depth === 1 && text[i] === ':') {
                  const key = JSON.parse(text.slice(start, end));
                  if (seen.has(key)) return false; seen.add(key);
                }
                i = end - 1;
              } else if (c === '{' || c === '[') ++depth;
              else if (c === '}' || c === ']') --depth;
            }
            return true;
          };
          const errorPacket = (p, code) => ({version:1,type:'error',id:p.id,document:p.document,error:{code}});
          const send = packet => {
            if (retired) return;
            try { modernFormsWebView.postMessage(JSON.stringify(packet)); } catch (_) { /* No exception details cross the bridge. */ }
          };
          const finish = (key, call, packet) => {
            if (retired || pending.get(key) !== call) return;
            pending.delete(key); clearTimeout(call.timer); send(packet);
          };
          const receive = event => {
            if (retired || typeof event.data !== 'string' || !sizeOK(event.data)) return;
            let p;
            try { p = JSON.parse(event.data); if (!depthOK(p) || !uniqueEnvelopeKeys(event.data)) return; }
            catch (_) { return; }
            if (!p || p.version !== 1 || !idOK(p.id) || !documentOK(p.document)) return;
            const key = p.document + '/' + p.id;
            if (p.type === 'cancel') {
              if (!fields(p, ['version','type','id','document'])) return;
              const call = pending.get(key);
              if (call) { pending.delete(key); clearTimeout(call.timer); call.controller.abort(); }
              return;
            }
            if (p.type !== 'request' || !fields(p, ['version','type','id','document','method','payload','timeoutMs']) ||
                !methodOK(p.method) || !Number.isInteger(p.timeoutMs) || p.timeoutMs < 1 || p.timeoutMs > 120000) return;
            if (pending.has(key) || recent.has(key)) return;
            if (pending.size >= maxPending) { send(errorPacket(p,'busy')); return; }
            // Bound duplicate suppression to this document's 256 most recent accepted IDs.
            // This is correlation, not an exactly-once or authenticated delivery guarantee.
            recent.add(key); if (recent.size > 256) recent.delete(recent.values().next().value);
            const handler = handlers.get(p.method);
            if (!handler) { send(errorPacket(p,'method_not_found')); return; }
            const controller = new AbortController(), call = {controller,timer:null};
            pending.set(key, call);
            call.timer = setTimeout(() => {
              finish(key, call, errorPacket(p,'timeout')); controller.abort();
            }, p.timeoutMs);
            Promise.resolve().then(() => {
              if (pending.get(key) !== call) return;
              return handler(p.payload, controller.signal);
            }).then(value => {
              if (pending.get(key) !== call) return;
              let text, packet;
              try {
                packet = {version:1,type:'result',id:p.id,document:p.document,payload:value};
                text = JSON.stringify(packet);
                if (!sizeOK(text)) { finish(key,call,errorPacket(p,'result_too_large')); return; }
                packet = JSON.parse(text);
                if (!Object.prototype.hasOwnProperty.call(packet,'payload') || !depthOK(packet))
                  throw new Error();
              } catch (_) { finish(key,call,errorPacket(p,'invalid_result')); return; }
              finish(key,call,packet);
            }, () => finish(key,call,errorPacket(p,'handler_error')));
          };
          window.addEventListener('message', receive);
          window.addEventListener('pagehide', () => {
            retired = true; window.removeEventListener('message', receive);
            const calls = [...pending.values()]; pending.clear(); recent.clear(); handlers.clear();
            for (const call of calls) { clearTimeout(call.timer); call.controller.abort(); }
            if (window.modernFormsRpc === api) delete window.modernFormsRpc;
          }, {once:true});
          const api = Object.freeze({version:1,
            register(name, handler) {
              if (retired || !methodOK(name) || typeof handler !== 'function') throw new TypeError('Invalid RPC registration');
              if (handlers.has(name)) throw new Error('RPC method already registered');
              if (handlers.size >= maxPending) throw new Error('RPC handler limit reached');
              handlers.set(name,handler);
            },
            unregister(name) { return handlers.delete(name); }
          });
          window.modernFormsRpc = api;
          return true;
        })()
        """;
}
