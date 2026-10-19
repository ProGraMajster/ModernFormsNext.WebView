using ModernFormsNext.WebView.Windows;
using ModernFormsNext.WebView.Sample;
using ModernFormsNext.WindowKit.Backend.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        WindowsPlatformBootstrap.Initialize();
        bool keyboard = args.Contains("--validate-keyboard", StringComparer.Ordinal);
        bool desktop = args.Contains("--validate-desktop", StringComparer.Ordinal);
        bool securityOnly = args.Contains("--validate-security", StringComparer.Ordinal);
        bool validate = keyboard || desktop || securityOnly || args.Contains("--validate", StringComparer.Ordinal);
        string? profile = validate ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mfn-webview-" + Guid.NewGuid().ToString("N")) : null;
        WindowsWebViewProvider.Register(new WindowsWebViewOptions { UserDataFolder = profile });
        using var form = new SampleForm();
        if (validate) form.Shown += async (_, _) =>
        {
            try
            {
                if (securityOnly) await RuntimeValidation.ReadyAsync(form.Browser);
                else await RuntimeValidation.RunAsync(form, Console.WriteLine);
                await DefaultSecurityRuntimeValidation.RunAsync(form.Browser, Console.WriteLine);
                string? tlsProbe = Environment.GetEnvironmentVariable("MFN_QA_TLS_URI");
                if (tlsProbe is not null) await DefaultSecurityRuntimeValidation.TlsAsync(form.Browser, new Uri(tlsProbe), Console.WriteLine);
                string? downloadProbe = Environment.GetEnvironmentVariable("MFN_QA_DOWNLOAD_URI");
                if (downloadProbe is not null)
                {
                    var address = new Uri(downloadProbe);
                    RuntimeValidation.Require(address.Scheme == "http" && address.Host == "127.0.0.1", "owned download fixture only");
                    form.Browser.Navigate(address);
                    await Task.Delay(1000);
                    Console.WriteLine("DOWNLOAD attempted; driver must prove server request and bounded no-file result");
                    await RuntimeValidation.NavigateAsync(form.Browser, () => form.Browser.NavigateToHtml("<p>Download denial recovery</p>"));
                }
                // Keyboard injection requires an interactive foreground desktop. Keep it
                // explicit so ordinary native validation can run from background tools.
                if (desktop) await NativeDesktopValidation.RunAsync(form);
                if (keyboard) await NativeKeyboardValidation.RunAsync(form);
                if (securityOnly) { Console.WriteLine("RUNTIME PASS (focused native security/desktop lane)"); return; }
                await WindowsMessagingValidation.RunAsync(form.Browser);
                await ValidateInitializationRacesAsync(form);
                await using var server = new LoopbackContentServer();
                await MessageSecurityRuntimeValidation.RunNetworkAsync(form.Browser, server.Address("security/redirect"),
                    new Uri(server.Address("security/frame").AbsoluteUri.Replace("127.0.0.1", "localhost", StringComparison.Ordinal)), Console.WriteLine);
                await NavigationRuntimeValidation.RunAsync(form.Browser, server.Address("one"), server.Address("two"), Console.WriteLine);
                await NavigationRuntimeValidation.RunPostAsync(form.Browser, server.Address("post"), Console.WriteLine);
                await NavigationSchemeRuntimeValidation.RunPostAsync(form.Browser, server.Address("post"), Console.WriteLine);
                await NavigationSchemeRuntimeValidation.RunRedirectAsync(form.Browser, server.Address("scheme/redirect"), Console.WriteLine);
                await ValidateInitializationFailureAsync(form);
                using var second = new ModernFormsNext.Form { UseSystemDecorations = true };
                second.Show();
                form.Browser.AllowedNavigationSchemes = ModernFormsNext.WebView.WebViewNavigationSchemes.Https |
                    ModernFormsNext.WebView.WebViewNavigationSchemes.AboutBlank;
                await RuntimeValidation.NavigateAsync(form.Browser, () => form.Browser.NavigateToLocalContent(LocalContentSample.Create()));
                using var reparentTransport = new ModernFormsNext.WebView.WebViewTransport(form.Browser);
                using var reparentRpc = new ModernFormsNext.WebView.WebViewRpcClient(reparentTransport);
                await reparentTransport.ExecuteScriptAsync(ModernFormsNext.WebView.WebViewRpcProtocol.JavaScript);
                await reparentTransport.ExecuteScriptAsync("modernFormsRpc.register('never',()=>new Promise(()=>{}));null");
                var retiredRpcCall = reparentRpc.InvokeAsync("never", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32);
                long before = form.Browser.GetDiagnostics().PeerGeneration;
                await RuntimeValidation.NavigateAsync(form.Browser, () => second.Controls.Add(form.Browser));
                try { await retiredRpcCall; throw new InvalidOperationException("Expected RPC cancellation on native reparent."); }
                catch (OperationCanceledException) { }
                await reparentTransport.ExecuteScriptAsync(ModernFormsNext.WebView.WebViewRpcProtocol.JavaScript);
                await reparentTransport.ExecuteScriptAsync("modernFormsRpc.register('echo',()=>42);null");
                RuntimeValidation.Require(await reparentRpc.InvokeAsync("echo", 0, RpcSampleJson.Default.Int32, RpcSampleJson.Default.Int32) == 42,
                    "same RPC client recovers after real native host recreation and explicit endpoint installation");
                Console.WriteLine("PASS pending RPC canceled by real cross-window host retirement + same-client recovery");
                RuntimeValidation.Require(form.Browser.GetDiagnostics().PeerGeneration > before, "cross-window peer recreated");
                RuntimeValidation.Require(form.Browser.AllowedNavigationSchemes == (ModernFormsNext.WebView.WebViewNavigationSchemes.Https |
                    ModernFormsNext.WebView.WebViewNavigationSchemes.AboutBlank) &&
                    form.Browser.BrowserCapabilities.HasFlag(ModernFormsNext.WebView.WebViewCapabilities.NavigationSchemeFiltering),
                    "scheme policy and detected capability survive real cross-window peer recreation");
                Console.WriteLine("PASS scheme filter preserved across real cross-window recreation");
                RuntimeValidation.Require(!form.Browser.CanGoBack && !form.Browser.CanGoForward, "recreation resets browser history");
                await RuntimeValidation.NavigateAsync(form.Browser, () =>
                { form.Controls.Add(form.Browser); form.Controls.SetChildIndex(form.Browser, 0); });
                RuntimeValidation.Require(await form.Browser.ExecuteScriptAsync("window.localAnswer") == "42", "local resources replay after reparent");
                Console.WriteLine("PASS cross-window reparent + replay + history reset");
                await RuntimeValidation.NavigateAsync(form.Browser, () => form.Browser.NavigateToLocalContent(ApplicationContentSample.Create()));
                await LocalContentPolicyRuntimeValidation.AssertApplicationAsync(form.Browser);
                var priorPolicyDocument = reparentTransport.CurrentDocument;
                await RuntimeValidation.NavigateAsync(form.Browser, () => second.Controls.Add(form.Browser));
                RuntimeValidation.Require(priorPolicyDocument.CancellationToken.IsCancellationRequested, "application policy reparent revokes old canonical context");
                await LocalContentPolicyRuntimeValidation.AssertApplicationAsync(form.Browser);
                await RuntimeValidation.NavigateAsync(form.Browser, () =>
                { form.Controls.Add(form.Browser); form.Controls.SetChildIndex(form.Browser, 0); });
                await LocalContentPolicyRuntimeValidation.AssertApplicationAsync(form.Browser);
                Console.WriteLine("PASS native application policy self fetch/base across cross-window peer recreation");
                Console.WriteLine("RUNTIME PASS");
            }
            catch (Exception error) { Console.WriteLine("RUNTIME FAIL: " + error); Environment.ExitCode = 1; }
            finally { form.Close(); }
        };
        ModernFormsNext.Application.Run(form);
        // Close ends the window loop; dispose the owned control tree before attempting
        // profile removal. The using declaration otherwise runs only after cleanup.
        form.Dispose();
        if (profile is not null)
        {
            string resolvedProfile = System.IO.Path.GetFullPath(profile);
            string temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)
                + System.IO.Path.DirectorySeparatorChar;
            if (!resolvedProfile.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !System.IO.Path.GetFileName(resolvedProfile).StartsWith("mfn-webview-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the test-owned temporary profile.");
            // Runtime subprocesses release their profile asynchronously after controller.Close.
            // Cleanup is bounded and only ever targets the unique test-owned directory.
            for (int i = 0; i < 20 && Directory.Exists(profile); ++i)
            {
                try { Directory.Delete(resolvedProfile, recursive: true); }
                catch (IOException) { Thread.Sleep(100); }
                catch (UnauthorizedAccessException) { Thread.Sleep(100); }
            }
            if (Directory.Exists(profile)) { Console.WriteLine("PROFILE CLEANUP FAIL"); Environment.ExitCode = 1; }
        }
    }

    private static async Task ValidateInitializationFailureAsync(SampleForm form)
    {
        string missingRuntime = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mfn-webview-missing-runtime-" + Guid.NewGuid().ToString("N"));
        string? previousRuntime = Environment.GetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER");
        // The SDK's process-local fixed-version override fails before creating a controller.
        // An invalid profile instead displays an Edge-owned modal dialog and is unsuitable
        // for unattended validation. Never alter the installed runtime or machine settings.
        RuntimeValidation.Require(!Directory.Exists(missingRuntime), "missing-runtime fixture has no browser executable");
        var provider = new WindowsWebViewProvider();
        using var browser = new ModernFormsNext.WebView.WebView(provider) { Bounds = new(10, 250, 100, 80) };
        var failure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int failures = 0;
        browser.InitializationFailed += (_, e) =>
        { RuntimeValidation.Require(e.Category == ModernFormsNext.WebView.WebViewErrorCategory.Engine, "neutral initialization failure"); ++failures; failure.TrySetResult(); };
        try
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", missingRuntime);
            form.Controls.Add(browser);
            RuntimeValidation.Require(browser.HostingDiagnostics.State == ModernFormsNext.NativeViewHostState.Attached,
                "failure fixture is visibly hosted inside the Form client area");
            await failure.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await ModernFormsNext.WindowKit.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
            RuntimeValidation.Require(failures == 1 && browser.InitializationState == ModernFormsNext.WebView.WebViewInitializationState.Failed,
                "failed peer does not automatically retry");
            RuntimeValidation.Require(form.Browser.IsInitialized, "initialization failure isolated from the active browser");
            Console.WriteLine("PASS isolated native initialization failure + no automatic retry");
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", previousRuntime);
            form.Controls.Remove(browser);
        }
    }

    private static async Task ValidateInitializationRacesAsync(SampleForm form)
    {
        using (var pending = new ModernFormsNext.WebView.WebView { Bounds = new(10, 250, 100, 80) })
        {
            form.Controls.Add(pending);
            try
            {
                RuntimeValidation.Require(!pending.IsInitialized, "controller initialization is pending");
                pending.Select(); form.Browser.Select();
                await RuntimeValidation.ReadyAsync(pending);
                RuntimeValidation.Require(form.Browser.Selected && !pending.Selected, "deferred controller does not steal canonical focus");
            }
            finally { form.Controls.Remove(pending); }
        }
        for (int i = 0; i < 10; ++i)
        {
            using var pending = new ModernFormsNext.WebView.WebView { Bounds = new(10, 250, 100, 80) };
            form.Controls.Add(pending); form.Controls.Remove(pending);
        }
        RuntimeValidation.Require(form.Browser.IsInitialized, "immediate retirement isolates pending controller initialization");
        Console.WriteLine("PASS deferred focus guard + 10 controller retirements during initialization");
    }
}
