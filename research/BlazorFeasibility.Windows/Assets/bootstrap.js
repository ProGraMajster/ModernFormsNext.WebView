'use strict';
window.probeViolations = [];
addEventListener('securitypolicyviolation', e => {
  if (probeViolations.length < 16) probeViolations.push(e.violatedDirective);
});
window.external = {
  sendMessage: message => modernFormsWebView.postMessage(message),
  receiveMessage: callback => addEventListener('message', e => callback(e.data))
};
