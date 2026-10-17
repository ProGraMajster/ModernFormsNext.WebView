using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModernFormsNext.WebView.Sample;

internal static class WindowsMessagingValidation
{
    internal static async Task RunAsync(WebView browser)
    {
        long id = 0;
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => id = e.RequestId;
        var messages = new List<string>();
        var fence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) =>
        {
            RuntimeValidation.Require(!e.IsOriginAuthenticated, "Windows tokens never upgrade original-caller trust");
            messages.Add(e.Message); if (e.Message == "guard-fence") fence.TrySetResult();
        };
        browser.NavigationStarting += starting; browser.WebMessageReceived += received;
        try
        {
            await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(LocalContentSample.Create()));
            string original = await CaptureAsync(browser);
            await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "guard local reload"));
            string current = await CaptureAsync(browser);
            var forged = JsonNode.Parse(original)!.AsArray(); var live = JsonNode.Parse(current)!.AsArray();
            RuntimeValidation.Require(forged.Count == 3 && forged[1]!.GetValue<string>() != live[1]!.GetValue<string>(), "document token changes across same-origin reload");
            forged[0] = id; forged[2] = "guard-forged-current-id";
            var guessed = live.DeepClone().AsArray(); guessed[1] = new string('0', 32); guessed[2] = "guard-guessed-token";
            await browser.ExecuteScriptAsync("chrome.webview.postMessage(" + JsonSerializer.Serialize(forged.ToJsonString()) + ");" +
                "chrome.webview.postMessage(" + JsonSerializer.Serialize(guessed.ToJsonString()) + ");" +
                "chrome.webview.postMessage(" + JsonSerializer.Serialize("[" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"guard-legacy\"]") + ");" +
                "modernFormsWebView.postMessage('guard-fence');null");
            await fence.Task.WaitAsync(TimeSpan.FromSeconds(10));
            RuntimeValidation.Require(!messages.Any(m => m is "guard-forged-current-id" or "guard-guessed-token" or "guard-legacy"), "old token cannot be rebound to public current ID");
            Console.WriteLine("PASS native Windows same-origin reload + captured old packet/current-ID substitution + guessed/legacy rejection");

            EventHandler<WebViewNavigationStartingEventArgs> toggle = (_, _) =>
            { browser.IsJavaScriptEnabled = false; browser.IsJavaScriptEnabled = true; };
            browser.NavigationStarting += toggle;
            try { await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(LocalContentSample.Create())); }
            finally { browser.NavigationStarting -= toggle; }
            RuntimeValidation.Require(!browser.IsLoading, "revoked helper does not strand navigation completion");
            await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "guard recovery after JS change during Starting"));
            Console.WriteLine("PASS native Windows JS revocation during Starting settles navigation without restoring token");

            browser.Stop();
            await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "guard reload after Stop"));
            browser.IsJavaScriptEnabled = false; browser.IsJavaScriptEnabled = true;
            await RuntimeValidation.NavigateAsync(browser, () => RuntimeValidation.Require(browser.Reload(), "guard reload after JS setting change"));
            fence = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await browser.ExecuteScriptAsync("modernFormsWebView.postMessage('guard-fence');null");
            await fence.Task.WaitAsync(TimeSpan.FromSeconds(10));
            RuntimeValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "Windows authenticated messaging remains unavailable");
            Console.WriteLine("PASS native Windows guard Stop/JS-disable/reload + baseline recovery; privileged original-caller NO-GO");
        }
        finally { browser.NavigationStarting -= starting; browser.WebMessageReceived -= received; }
    }

    private static async Task<string> CaptureAsync(WebView browser)
    {
        // Capture our own real private packet without exposing it in logs. Rewriting only
        // its public ID models an old sender claiming the current logical document.
        string? packet = JsonSerializer.Deserialize<string>(await browser.ExecuteScriptAsync("""
            (()=>{const stringify=JSON.stringify;let packet;
            try {JSON.stringify=function(v){const text=stringify(v);if(Array.isArray(v)&&v[2]==='guard-capture')packet=text;return text;};
            modernFormsWebView.postMessage('guard-capture');return packet;}
            finally {JSON.stringify=stringify;}})()
            """));
        RuntimeValidation.Require(packet is not null, "native private helper capture"); return packet!;
    }
}
