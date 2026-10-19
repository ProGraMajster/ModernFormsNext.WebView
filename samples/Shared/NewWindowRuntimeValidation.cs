namespace ModernFormsNext.WebView.Sample;

internal static class NewWindowRuntimeValidation
{
    private const string Page = """
        <button id='popup-button' onclick='attemptPopup()'>Open owned popup</button><p id='source'>Popup actor</p>
        <script>
        window.popupAttempts=0;window.popupDone=false;window.captureOwnedPopup=false;
        function attemptPopup(){
          ++popupAttempts;
          window.lastPopup=window.open('about:blank','_blank');
          if(lastPopup){try{lastPopup.document.write('<p id="owned-popup">owned popup</p>');lastPopup.document.close();}catch(e){window.popupWriteError=String(e.name);}}
          if(window.captureOwnedPopup){window.ownedPopup=window.lastPopup;window.captureOwnedPopup=false;}
          window.popupDone=true;modernFormsWebView.postMessage('popup-actor-done');
        }
        addEventListener('message',function(e){if(e.data==='popup-attempt'||e.data==='popup-attempt-owned'){
          window.captureOwnedPopup=e.data==='popup-attempt-owned';setTimeout(attemptPopup,6000);
        }});
        </script>
        """;

    internal static async Task RunAsync(SampleForm form, Action<string> report, Func<Task>? nativeGesture = null)
    {
        WebView source = form.Browser;
        await RuntimeValidation.ReadyAsync(source);
        RuntimeValidation.Require(source.BrowserCapabilities.HasFlag(WebViewCapabilities.NewWindows), "installed native popup hooks");
        await RuntimeValidation.NavigateAsync(source, () => source.NavigateToHtml(Page));
        using var target = new WebView { Bounds = new(4, 4, 120, 70), IsJavaScriptEnabled = true };
        form.Controls.Add(target);
        await RuntimeValidation.ReadyAsync(target);
        using var targetTransport = new WebViewTransport(target);
        int offered = 0, warnings = 0;
        bool accept = true, reuseRejected = false;
        WebViewNewWindowRequestedEventArgs? retained = null;
        EventHandler<WebViewNewWindowRequestedEventArgs> handler = (_, e) =>
        {
            ++offered; retained = e;
            if (source.GetDiagnostics().Platform == "Android") RuntimeValidation.Require(e.RequestedUri is null, "Android does not invent a destination/origin");
            if (accept) e.OpenIn(target);
            else
            {
                try { e.OpenIn(target); }
                catch (InvalidOperationException) { reuseRejected = true; }
            }
        };
        EventHandler<WebViewSecurityWarningEventArgs> warning = (_, e) =>
        { RuntimeValidation.Require(e.DiagnosticId == "MFNWVSEC001" && e.Message.Contains("UserInitiated", StringComparison.Ordinal), "redacted native risk diagnostic"); ++warnings; };
        source.NewWindowRequested += handler; source.SecurityWarning += warning;
        try
        {
            source.NewWindowPolicy = WebViewNewWindowPolicy.Deny;
            await TimerAttemptAsync(source);
            RuntimeValidation.Require(offered == 0 && targetTransport.CurrentDocument.RequestId == 0, "default deny offers no target");
            // WebView2 may return a testing WindowProxy for a denied request. It is not
            // portable to assert only window.open === null as a native denial proof.
            report("PASS native parsed popup actor attempted; default deny offers no application target");
            source.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
            await TimerAttemptAsync(source);
            RuntimeValidation.Require(offered == 0 && targetTransport.CurrentDocument.RequestId == 0, "native non-gesture intent rejected by UserInitiated");
            RuntimeValidation.Require(warnings == 0, "safe selections do not emit security warnings");
            report("PASS native timer intent denied by UserInitiated; safe values have no warning");

            source.NewWindowPolicy = Enum.Parse<WebViewNewWindowPolicy>("AllowScriptInitiated");
            RuntimeValidation.Require(warnings == 1, "dynamic native unsafe policy emits warning");
            await TimerAttemptAsync(source, true);
            await WaitAsync(target, "!!document.getElementById('owned-popup')");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!targetTransport.CurrentDocument.IsReady && watch.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(20);
            report("POPUP target offered=" + offered + " ready=" + targetTransport.CurrentDocument.IsReady +
                " request=" + targetTransport.CurrentDocument.RequestId + " diagnostics=" + System.Text.Json.JsonSerializer.Serialize(target.GetDiagnostics()));
            RuntimeValidation.Require(offered == 1 && targetTransport.CurrentDocument.IsReady && targetTransport.CurrentDocument.RequestId > 0,
                "explicit target owns an actual native popup and canonical document");
            RuntimeValidation.Require(target.NewWindowPolicy == WebViewNewWindowPolicy.Deny &&
                !target.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "target does not inherit risky policy or authority");
            var message = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<WebViewMessageReceivedEventArgs> targetMessage = (_, e) =>
            { if (e.Message == "popup-target-message") { RuntimeValidation.Require(!e.IsOriginAuthenticated, "popup messages are untrusted"); message.TrySetResult(); } };
            target.WebMessageReceived += targetMessage;
            try
            {
                await target.ExecuteScriptAsync("modernFormsWebView.postMessage('popup-target-message');null");
                await message.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { target.WebMessageReceived -= targetMessage; }
            report("PASS actual native non-gesture popup handoff to prehosted fresh control + opener DOM write; shared profile/opener risk remains");
            RuntimeValidation.Require(retained is not null, "native decision event ran");
            bool expired = false;
            try { retained!.OpenIn(target); } catch (InvalidOperationException) { expired = true; }
            RuntimeValidation.Require(expired, "retained async acceptance is rejected");

            var document = targetTransport.CurrentDocument;
            accept = false;
            await TimerAttemptAsync(source);
            RuntimeValidation.Require(reuseRejected && offered == 2 && !document.CancellationToken.IsCancellationRequested &&
                await target.ExecuteScriptAsync("!!document.getElementById('owned-popup')") == "true", "used target is rejected without replacing its document");
            report("PASS native reused-target rejection preserves its existing document");

            var close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler closed = (_, _) => close.TrySetResult(); target.CloseRequested += closed;
            try
            {
                await source.ExecuteScriptAsync("setTimeout(function(){ownedPopup.close();},0);null");
                await close.Task.WaitAsync(TimeSpan.FromSeconds(15));
                RuntimeValidation.Require(target.IsInitialized && source.IsInitialized, "script close is informational; app still owns both controls");
            }
            finally { target.CloseRequested -= closed; }
            report("PASS actual native window.close notification; no automatic Form/control disposal");
        }
        finally
        {
            source.NewWindowRequested -= handler; source.SecurityWarning -= warning;
            source.NewWindowPolicy = WebViewNewWindowPolicy.Deny;
            form.Controls.Remove(target);
        }
        await ReentrantStopAsync(form, report);
        if (nativeGesture is not null) await GestureAsync(form, report, nativeGesture);
    }

    private static async Task GestureAsync(SampleForm form, Action<string> report, Func<Task> activate)
    {
        var source = form.Browser;
        await RuntimeValidation.NavigateAsync(source, () => source.NavigateToHtml(Page));
        using var target = new WebView { Bounds = new(4, 4, 120, 70), IsJavaScriptEnabled = true };
        form.Controls.Add(target); await RuntimeValidation.ReadyAsync(target);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNewWindowRequestedEventArgs> handler = (_, e) =>
        { RuntimeValidation.Require(e.IsUserInitiated, "actual SDK user gesture indication"); e.OpenIn(target); requested.TrySetResult(); };
        source.NewWindowRequested += handler; source.NewWindowPolicy = WebViewNewWindowPolicy.UserInitiated;
        try
        {
            source.Select(); await source.ExecuteScriptAsync("document.getElementById('popup-button').focus();null");
            await Task.Delay(6000); // Expire application-script activation before actual native Enter.
            await activate(); await requested.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await WaitAsync(target, "!!document.getElementById('owned-popup')");
            report("PASS native Enter activates HTML button and UserInitiated accepts explicit fresh target");
        }
        finally { source.NewWindowRequested -= handler; source.NewWindowPolicy = WebViewNewWindowPolicy.Deny; form.Controls.Remove(target); }
    }

    private static async Task ReentrantStopAsync(SampleForm form, Action<string> report)
    {
        var source = form.Browser;
        await RuntimeValidation.NavigateAsync(source, () => source.NavigateToHtml(Page));
        using var target = new WebView { Bounds = new(4, 4, 120, 70), IsJavaScriptEnabled = true };
        form.Controls.Add(target); await RuntimeValidation.ReadyAsync(target);
        using var transport = new WebViewTransport(target);
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNewWindowRequestedEventArgs> handler = (_, e) => { e.OpenIn(target); source.Stop(); executed.TrySetResult(); };
        source.NewWindowRequested += handler; source.NewWindowPolicy = (WebViewNewWindowPolicy)2;
        try
        {
            await source.PostMessageAsync("popup-attempt");
            await executed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            RuntimeValidation.Require(transport.CurrentDocument.RequestId == 0 && !transport.CurrentDocument.IsReady,
                "actual native reentrant Stop denies before consuming a target");
            report("PASS native decision reentrancy Stop invalidates canonical source before SDK handoff");
        }
        finally
        {
            source.NewWindowRequested -= handler; source.NewWindowPolicy = WebViewNewWindowPolicy.Deny;
            form.Controls.Remove(target);
        }
        await RuntimeValidation.NavigateAsync(source, () => source.NavigateToHtml("<p>Popup validation recovery</p>"));
    }
    private static async Task TimerAttemptAsync(WebView source, bool capture = false)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> handler = (_, e) =>
        {
            if (e.Message != "popup-actor-done") return;
            RuntimeValidation.Require(!e.IsOriginAuthenticated, "popup actor completion remains untrusted"); done.TrySetResult();
        };
        source.WebMessageReceived += handler;
        try
        {
            // Do not poll with ExecuteScript before the timer: WebView2's application
            // script execution can confer transient activation. The parsed page actor
            // waits beyond that activation and reports through the baseline bridge.
            await source.PostMessageAsync(capture ? "popup-attempt-owned" : "popup-attempt");
            await done.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { source.WebMessageReceived -= handler; }
    }
    private static async Task WaitAsync(WebView browser, string predicate)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15))
        { if (await browser.ExecuteScriptAsync("!!(" + predicate + ")") == "true") return; await Task.Delay(20); }
        throw new TimeoutException("Native popup actor/target did not complete: " + predicate);
    }
}
