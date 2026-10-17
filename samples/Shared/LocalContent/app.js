window.localAnswer = 42;
window.missingDenied = false;
const missingImage = new Image();
missingImage.onerror = function () { window.missingDenied = true; };
missingImage.src = 'missing.png';
window.policyViolations = [];
document.addEventListener('securitypolicyviolation', function (event) {
    window.policyViolations.push(event.effectiveDirective);
});
document.getElementById('result').textContent = 'External local JavaScript loaded. No server or directory mapping.';
document.getElementById('send').addEventListener('click', function () {
    if (window.modernFormsWebView) window.modernFormsWebView.postMessage('offline-untrusted-string');
});
