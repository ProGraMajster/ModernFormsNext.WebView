using System.Text.Json;

namespace ModernFormsNext.WebView.Sample;

internal static class RuntimeValidation
{
    internal static async Task RunAsync(SampleForm form, Action<string> report)
    {
        var browser = form.Browser;
        await ReadyAsync(browser);
        report("PASS initialize: " + JsonSerializer.Serialize(browser.GetDiagnostics()));
        await MessageSecurityRuntimeValidation.RunAsync(browser, report);
        await LocalContentRuntimeValidation.RunAsync(browser, report);
        await NavigateAsync(browser, () => browser.NavigateToHtml(TestContent.Html));
        report("PASS raw HTML + navigation events");
        string json = await browser.ExecuteScriptAsync("testResult()");
        using (var result = JsonDocument.Parse(json))
            Require(result.RootElement.GetProperty("answer").GetInt32() == 42, "JavaScript JSON result");
        report("PASS JavaScript JSON value");
        (string Script, string Json)[] values =
        [ ("'value'", "\"value\""), ("42", "42"), ("true", "true"), ("null", "null"),
          ("undefined", "null"), ("({answer:42})", "{\"answer\":42}"), ("[1,true,null]", "[1,true,null]"),
          ("throw new Error('validation');", "null") ];
        foreach (var value in values)
        {
            using var actual = JsonDocument.Parse(await browser.ExecuteScriptAsync(value.Script));
            using var expected = JsonDocument.Parse(value.Json);
            Require(JsonElement.DeepEquals(actual.RootElement, expected.RootElement),
                "native JavaScript result shape for fixture " + value.Script + ": " + actual.RootElement.GetRawText());
        }
        // Native serializers differ outside JSON's value domain. Keep execution native;
        // this qualification fixture records the documented cyclic-value behavior of
        // the installed engines instead of introducing eval/wrapper semantics.
        using (var cyclic = JsonDocument.Parse(await browser.ExecuteScriptAsync("(()=>{const x={};x.self=x;return x;})()")))
        {
            string expected = browser.GetDiagnostics().Platform == "Android" ? "{\"self\":null}" : "null";
            using var expectedValue = JsonDocument.Parse(expected);
            Require(JsonElement.DeepEquals(cyclic.RootElement, expectedValue.RootElement), "native cyclic result for installed engine");
        }
        report("PASS native script result matrix including undefined/exception/nonserializable");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> handler = (_, e) => received.TrySetResult(e.Message);
        browser.WebMessageReceived += handler;
        try
        {
            // Exercise malformed transport data through the real engine entry point,
            // then a valid helper message. Untrusted packet shapes must never crash JNI/COM.
            await browser.ExecuteScriptAsync("(function(){const send=typeof __modernFormsStringBridge!=='undefined'?" +
                "v=>__modernFormsStringBridge.postMessage(v):v=>chrome.webview.postMessage(v);" +
                "send(null);for(const v of [null,{},[],['id','payload'],[{},'payload'],[null,'payload']," +
                "[1.5,'payload'],[1,{}],[1],['bad']])send(JSON.stringify(v));" +
                "modernFormsWebView.postMessage('runtime-page-message');})();null");
            Require(await received.Task.WaitAsync(TimeSpan.FromSeconds(15)) == "runtime-page-message", "page to app string bridge");
        }
        finally { browser.WebMessageReceived -= handler; }
        report("PASS malformed bridge packets rejected + page to app messaging");
        const string payload = "app \"quoted\" \\ path\nZażółć 😀 \u2028 </script><script>alert('payload')</script>";
        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> ack = (_, e) => { if (e.Message == "ack") acknowledged.TrySetResult(); };
        browser.WebMessageReceived += ack;
        try
        {
            await browser.ExecuteScriptAsync("window.addEventListener('message',function(){modernFormsWebView.postMessage('ack');});null");
            await browser.PostMessageAsync(payload);
            await acknowledged.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Require(JsonSerializer.Deserialize<string>(await browser.ExecuteScriptAsync("receivedMessage")) == payload, "app to page escaping");
        }
        finally { browser.WebMessageReceived -= ack; }
        report("PASS app to page messaging + escaping");
        int canceled = 0;
        EventHandler<WebViewNavigationStartingEventArgs> cancel = (_, e) => { if (e.Uri?.Host == "blocked.example") { e.Cancel = true; ++canceled; } };
        browser.NavigationStarting += cancel;
        try
        {
            browser.Navigate(new Uri("https://blocked.example/"));
            // WebView2 Starting is asynchronous; fence it with the browser task/dispatcher
            // without assuming that Navigate synchronously emits its native notification.
            await WaitForAsync(browser, () => canceled > 0);
            Require(!browser.IsLoading && browser.Source is null, "canceled source/loading state");
        }
        finally { browser.NavigationStarting -= cancel; }
        report("PASS cancellation before remote navigation");
        await NavigateAsync(browser, () => Require(browser.Reload(), "reload available"));
        browser.Stop(); Require(!browser.IsLoading, "Stop settles loading");
        report("PASS reload + stop");
        browser.Width = Math.Max(100, browser.Width - 20);
        browser.Height = Math.Max(100, browser.Height - 20);
        browser.Visible = false; browser.Visible = true;
        Require(browser.HostingDiagnostics.Placement.Visible, "native visibility restored");
        browser.Select();
        await browser.ExecuteScriptAsync("document.getElementById('editor').focus();null");
        Require(await browser.ExecuteScriptAsync("document.activeElement.id") == "\"editor\"", "native input focus");
        report("PASS resize + hide/show + native page input focus");
        using (var viewport = form.Controls.Add(new Panel { Bounds = new(30, 160, 220, 150), AutoScroll = true }))
        {
            browser.Dock = DockStyle.None;
            viewport.Controls.Add(browser); browser.Bounds = new(170, 10, 220, 120);
            RuntimeValidation.Require(browser.HostingDiagnostics.Placement.PixelClip.Width < browser.HostingDiagnostics.Placement.PixelBounds.Width,
                "ancestor rectangular native clipping");
            int beforeScroll = browser.HostingDiagnostics.Placement.PixelBounds.X;
            viewport.HorizontalScrollProperties.Value = 100;
            RuntimeValidation.Require(browser.HostingDiagnostics.Placement.PixelBounds.X < beforeScroll, "framework scrolling moves native content");
            form.Controls.Add(browser); form.Controls.SetChildIndex(browser, 0); browser.Dock = DockStyle.Fill;
        }
        report("PASS ancestor scroll + rectangular native placement clipping");
        using (var modal = new Form { Size = new(300, 200), Text = "MFN modal overlay" })
        {
            modal.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "MFN overlay above the owner's browser" });
            Task<DialogResult> closing = modal.ShowDialog(form);
            Require(!browser.HostingDiagnostics.Placement.Enabled, "modal disables native owner input");
            modal.Close(); await closing;
            Require(browser.HostingDiagnostics.Placement.Enabled, "modal restores native owner input");
        }
        report("PASS MFN modal overlay lifecycle + native owner input isolation");
        var generation = browser.GetDiagnostics().PeerGeneration;
        // Disposing a peer through a cross-window move is exercised separately on Windows.
        // Independent controls test the real provider/controller lifecycle on both platforms.
        for (int i = 0; i < 50; ++i)
        {
            var temporary = form.Controls.Add(new WebView { Bounds = new(2, 2, 80, 60), IsJavaScriptEnabled = true });
            try { await ReadyAsync(temporary); await NavigateAsync(temporary, () => temporary.NavigateToHtml("<p>lease</p>")); }
            finally { form.Controls.Remove(temporary); temporary.Dispose(); }
        }
        Require(browser.GetDiagnostics().PeerGeneration == generation, "independent leases preserve active peer");
        report("PASS 50 sequential real browser create/dispose leases");
    }

    internal static async Task ReadyAsync(WebView browser)
    {
        if (browser.IsInitialized) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler ready = (_, _) => { if (browser.IsInitialized) completion.TrySetResult(); };
        EventHandler<WebViewInitializationFailedEventArgs> failure = (_, e) => completion.TrySetException(new WebViewOperationException(e.Category));
        browser.InitializationStateChanged += ready; browser.InitializationFailed += failure;
        try { if (browser.IsInitialized) completion.TrySetResult(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { browser.InitializationStateChanged -= ready; browser.InitializationFailed -= failure; }
    }

    internal static async Task NavigateAsync(WebView browser, Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationCompletedEventArgs> done = (_, _) => completion.TrySetResult();
        EventHandler<WebViewNavigationFailedEventArgs> failed = (_, e) => completion.TrySetException(new WebViewOperationException(e.Category));
        browser.NavigationCompleted += done; browser.NavigationFailed += failed;
        try { action(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(30)); }
        finally { browser.NavigationCompleted -= done; browser.NavigationFailed -= failed; }
    }

    private static async Task WaitForAsync(WebView browser, Func<bool> predicate)
    {
        if (predicate()) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler change = (_, _) => { if (predicate()) completion.TrySetResult(); };
        browser.StateChanged += change;
        try { if (predicate()) completion.TrySetResult(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { browser.StateChanged -= change; }
    }
    internal static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Runtime assertion failed: " + message); }
}
