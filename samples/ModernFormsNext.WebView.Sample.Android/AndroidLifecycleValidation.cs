using System.Diagnostics;
using ModernFormsNext.WindowKit.Backend.Android;

namespace ModernFormsNext.WebView.Sample;

// Uses the framework's weak Activity tracker and retained Form. No test-owned Activity
// registry, alternate host, identity counter or browser-state restoration is installed.
internal static class AndroidLifecycleValidation
{
    internal static async Task RunAsync(SampleForm form, bool modernRpc, Action<string> report)
    {
        WebView browser = form.Browser;
        browser.UserAgent = "MFN-Platform-QA/1";
        var content = modernRpc ? ApplicationContentSample.Create() : BaselineFloorRuntimeValidation.CreateContent();
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToLocalContent(content));
        using var transport = new WebViewTransport(browser);
        using var rpc = new WebViewRpcClient(transport);
        Task<int>? pendingRpc = null;
        if (modernRpc)
        {
            RuntimeValidation.Require(await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript) == "true", "lifecycle real RPC installation");
            await transport.ExecuteScriptAsync("modernFormsRpc.register('never',function(){window.qaRpcStarted=true;return new Promise(function(){});});null");
            pendingRpc = rpc.InvokeAsync("never", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32, timeout: TimeSpan.FromSeconds(30));
            await BaselineFloorRuntimeValidation.WaitAsync(browser, "window.qaRpcStarted===true");
            RuntimeValidation.Require(!pendingRpc.IsCompleted, "RPC really pending before Activity recreation");
        }
        var delayedStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewMessageReceivedEventArgs> started = (_, e) =>
        { if (e.Message == "qa-delayed-start") { RuntimeValidation.Require(!e.IsOriginAuthenticated, "delayed actor untrusted"); delayedStarted.TrySetResult(); } };
        browser.WebMessageReceived += started;
        Task<string> delayed;
        try
        {
            // The native script positively announces execution before bounded renderer work.
            // Disposal may discard its SDK reply entirely; no late-callback arrival is inferred.
            delayed = browser.ExecuteScriptAsync("(function(){modernFormsWebView.postMessage('qa-delayed-start');var until=Date.now()+500;while(Date.now()<until){};return 42;})();");
            await delayedStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { browser.WebMessageReceived -= started; }
        long generation = browser.GetDiagnostics().PeerGeneration;
        var oldDocument = transport.CurrentDocument;
        var activity = AndroidWindowKit.Current.ActivityTracker.CurrentActivity ?? throw new InvalidOperationException("No resumed test Activity");
        var oldActivity = new WeakReference<global::Android.App.Activity>(activity);
        using var revoked = oldDocument.CancellationToken.Register(() => report("LIFECYCLE canonical document revoked before native peer destruction"));
        activity.Recreate(); activity = null;
        if (pendingRpc is not null) await CanceledAsync(pendingRpc);
        await CanceledAsync(delayed);
        await FreshAsync(browser, generation, transport);
        var replacement = AndroidWindowKit.Current.ActivityTracker.CurrentActivity;
        RuntimeValidation.Require(replacement is not null && (!oldActivity.TryGetTarget(out var previous) || !ReferenceEquals(previous, replacement)), "fresh Activity identity");
        RuntimeValidation.Require(oldDocument.CancellationToken.IsCancellationRequested && browser.Source == content.GetUri("index.html"), "recreated peer replays exact collection");
        RuntimeValidation.Require(!browser.CanGoBack && !browser.CanGoForward, "Activity replay resets native history");
        RuntimeValidation.Require(browser.UserAgent == "MFN-Platform-QA/1" && await browser.ExecuteScriptAsync("navigator.userAgent") == "\"MFN-Platform-QA/1\"", "native UserAgent survives recreation");
        if (modernRpc)
        {
            await LocalContentPolicyRuntimeValidation.AssertApplicationAsync(browser);
            RuntimeValidation.Require(await transport.ExecuteScriptAsync("typeof modernFormsRpc") == "\"undefined\"", "old RPC endpoint does not survive fresh document");
            await transport.ExecuteScriptAsync(WebViewRpcProtocol.JavaScript);
            await transport.ExecuteScriptAsync("modernFormsRpc.register('echo',function(){return 42;});null");
            RuntimeValidation.Require(await rpc.InvokeAsync("echo", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32) == 42, "same RPC client recovers on fresh Activity/document");
        }
        else await BaselineFloorRuntimeValidation.WaitAsync(browser, "window.floorLocal===42 && !!window.floorEvalDenied && !window.floorInline");
        report("PASS actual Activity.Recreate + fresh peer/document + canceled live renderer work + collection/CSP/settings" + (modernRpc ? " + pending RPC/same-client recovery" : " (modern RPC not selected in floor lane)"));
        report("MEMORY bounded observation managed=" + GC.GetTotalMemory(false) + " native=" + global::Android.OS.Debug.NativeHeapAllocatedSize +
            " javaTotal=" + Java.Lang.Runtime.GetRuntime()!.TotalMemory() + "; not a retained-Activity/heap-soak proof");
        if (modernRpc) await InterruptedPostAsync(browser, transport, report);
    }

    private static async Task InterruptedPostAsync(WebView browser, IWebViewTransport transport, Action<string> report)
    {
        await RuntimeValidation.NavigateAsync(browser, () => browser.NavigateToHtml("<form method='post' action='https://example.com/?mfn-qa-post=1'><input name='qa' value='bounded-fixture'></form>"));
        long generation = browser.GetDiagnostics().PeerGeneration;
        var recovery = ApplicationContentSample.Create();
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) =>
        {
            if (e.Uri?.Query != "?mfn-qa-post=1") return;
            observed.TrySetResult();
            (AndroidWindowKit.Current.ActivityTracker.CurrentActivity ?? throw new InvalidOperationException("No POST Activity")).Recreate();
        };
        EventHandler initialized = (_, _) =>
        {
            if (browser.IsInitialized && browser.GetDiagnostics().PeerGeneration > generation)
                browser.NavigateToLocalContent(recovery); // App deliberately replaces the interrupted POST; no POST/body replay claim.
        };
        browser.NavigationStarting += starting; browser.InitializationStateChanged += initialized;
        try
        {
            Task<string> submission = browser.ExecuteScriptAsync("document.forms[0].submit();null");
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            try { await submission; } catch (OperationCanceledException) { }
            await FreshAsync(browser, generation, transport);
            await LocalContentPolicyRuntimeValidation.AssertApplicationAsync(browser);
            await Task.Delay(500);
            RuntimeValidation.Require(browser.Source == recovery.GetUri("index.html"), "interrupted old POST cannot overwrite fresh collection");
            report("PASS actual POST OnPageStarted -> Activity recreation -> explicit fresh collection; late reply arrival unobserved, server side effects possible");
        }
        finally { browser.NavigationStarting -= starting; browser.InitializationStateChanged -= initialized; }
    }

    private static async Task FreshAsync(WebView browser, long generation, IWebViewTransport transport)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (browser.IsInitialized && browser.GetDiagnostics().PeerGeneration > generation && transport.CurrentDocument.IsReady) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Activity did not produce a fresh ready canonical peer/document");
    }
    private static async Task CanceledAsync(Task operation)
    {
        try { await operation.WaitAsync(TimeSpan.FromSeconds(10)); throw new InvalidOperationException("Retired operation completed instead of cancellation"); }
        catch (OperationCanceledException) { }
    }
}
