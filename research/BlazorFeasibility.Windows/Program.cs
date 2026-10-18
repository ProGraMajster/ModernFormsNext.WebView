using System.Drawing;
using Microsoft.AspNetCore.Components;
using ModernFormsNext.WebView.Windows;
using ModernFormsNext.WindowKit.Backend.Windows;

namespace ModernFormsNext.WebView.BlazorProbe;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ContractValidation.RunAsync().GetAwaiter().GetResult();
            if (args.Contains("--validate-contracts", StringComparer.Ordinal)) return;
            using var services = RazorSnapshots.CreateServices();
            // Render immutable snapshots before the native loop starts. This experiment
            // does not add an app dispatcher or synchronously block an active UI loop.
            var snapshots = new[] { new ResourceCatalog(RazorSnapshots.RenderAsync(services, 0).GetAwaiter().GetResult()),
                new ResourceCatalog(RazorSnapshots.RenderAsync(services, 1).GetAwaiter().GetResult()) };
            bool validate = args.Contains("--validate", StringComparer.Ordinal);
            // The validator owns the temporary profile for the whole process lifetime.
            // The provider's application-owned environment can outlive its last controller.
            int profileArgument = Array.IndexOf(args, "--profile");
            string? profile = null;
            if (validate)
            {
                if (profileArgument < 0 || profileArgument + 1 >= args.Length)
                    throw new ArgumentException("Run native validation through this project's Validate.ps1.");
                profile = args[profileArgument + 1];
            }
            WindowsPlatformBootstrap.Initialize();
            WindowsWebViewProvider.Register(new WindowsWebViewOptions { UserDataFolder = profile });
            using var form = new ProbeForm(snapshots);
            form.Shown += async (_, _) =>
            {
                try
                {
                    await ReadyAsync(form.Browser, form.Lifetime);
                    if (validate) await ValidateNativeAsync(form, snapshots, services);
                    else await NavigateAsync(form.Browser, () => form.Browser.NavigateToLocalContent(snapshots[0].Content), form.Lifetime);
                }
                catch (OperationCanceledException) when (form.Lifetime.IsCancellationRequested) { }
                catch (Exception error) { Console.WriteLine("FEASIBILITY FAIL: " + error.GetType().Name + ": " + error.Message); Environment.ExitCode = 1; }
                finally { if (validate && !form.Lifetime.IsCancellationRequested) form.Close(); }
            };
            Application.Run(form); form.Dispose();
        }
        catch (Exception error) { Console.WriteLine("FEASIBILITY FAIL: " + error.GetType().Name + ": " + error.Message); Environment.ExitCode = 1; }
    }

    private static async Task ValidateNativeAsync(ProbeForm form, ResourceCatalog[] snapshots, IServiceProvider services)
    {
        var browser = form.Browser;
        using var transport = new WebViewTransport(browser);
        var dispatcher = new MfnComponentDispatcher();
        bool ui = await Task.Run(() => dispatcher.InvokeAsync(() => dispatcher.CheckAccess()));
        ContractValidation.Require(ui, "ASP.NET dispatcher delegates to existing MFN UI thread");
        await using (var manager = new ManagerProbe(services, dispatcher, snapshots[0],
            _ => browser.NavigateToLocalContent(snapshots[0].Content), transport))
        {
            foreach (string path in snapshots[0].Paths) manager.CheckResource(path);
            EventHandler<WebViewMessageReceivedEventArgs> received = (_, e) => manager.RejectUntrusted(e);
            browser.WebMessageReceived += received;
            try
            {
                await manager.AddRootComponentAsync(typeof(ProbeRoot), "#interactive-root", ParameterView.Empty);
                await NavigateAsync(browser, () => manager.Navigate("index.html"), form.Lifetime);
                ContractValidation.Require(await browser.ExecuteScriptAsync("document.getElementById('razor-count').textContent") == "\"0\"", "real compiled Razor DOM");
                ContractValidation.Require(await browser.ExecuteScriptAsync("typeof Blazor.start") == "\"function\"", "official package JavaScript loaded");
                ContractValidation.Require(await browser.ExecuteScriptAsync("document.baseURI===location.href") == "true", "fixed base-uri policy rejects supplied base element");
                // Execute the unmodified public boot API. Its failed fetch must have a
                // positive CSP violation, not a missing asset or a mocked JS response.
                await browser.ExecuteScriptAsync("Blazor.start().then(()=>window.probeStart='started',()=>window.probeStart='blocked');null");
                await WaitScriptAsync(browser, "window.probeStart==='blocked'&&probeViolations.some(x=>x==='connect-src')", form.Lifetime);
                await browser.ExecuteScriptAsync("modernFormsWebView.postMessage('__bwv:[\"AttachPage\",\"claimed-source\"]');null");
                await WaitAsync(() => manager.RejectedMessages > 0, form.Lifetime);
                ContractValidation.Require(!await manager.TryDispatchAsync(_ => throw new InvalidOperationException("Unverified native scope dispatch.")), "Blazor page context remains unattached");
                ContractValidation.Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "no privileged capability promotion");
                Console.WriteLine("PASS native public manager assets/dispatcher + official Blazor.start blocked by connect-src CSP + base-uri denial + untrusted IPC rejected");
            }
            finally { browser.WebMessageReceived -= received; }
        }
        await NavigateAsync(browser, form.NextSnapshot.PerformClick, form.Lifetime);
        ContractValidation.Require(await browser.ExecuteScriptAsync("document.getElementById('razor-count').textContent") == "\"1\"", "native MFN button selects newly rendered Razor parameter snapshot");
        await NavigateAsync(browser, () => ContractValidation.Require(browser.Reload(), "passive Razor reload"), form.Lifetime);
        ContractValidation.Require(await browser.ExecuteScriptAsync("document.getElementById('razor-count').textContent") == "\"1\"", "passive snapshot lifetime/reload");
        Console.WriteLine("PASS native MFN controls + passive compiled Razor DOM/parameters/independent origins/reload; renderer/scoped DI disposal validated");
        Console.WriteLine("FEASIBILITY EXECUTED; INTERACTIVE BLAZOR NO-GO under current content/security contract");
    }

    private static async Task ReadyAsync(WebView browser, CancellationToken token)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler changed = (_, _) => { if (browser.IsInitialized) ready.TrySetResult(); else if (browser.InitializationState == WebViewInitializationState.Failed) ready.TrySetException(new InvalidOperationException("Native initialization failed.")); };
        browser.InitializationStateChanged += changed;
        try { changed(null, EventArgs.Empty); await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), token); }
        finally { browser.InitializationStateChanged -= changed; }
    }
    private static async Task NavigateAsync(WebView browser, Action action, CancellationToken token)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationCompletedEventArgs> done = (_, _) => completed.TrySetResult();
        EventHandler<WebViewNavigationFailedEventArgs> failed = (_, _) => completed.TrySetException(new InvalidOperationException("Native research navigation failed."));
        browser.NavigationCompleted += done; browser.NavigationFailed += failed;
        try { action(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), token); }
        finally { browser.NavigationCompleted -= done; browser.NavigationFailed -= failed; }
    }
    private static async Task WaitScriptAsync(WebView browser, string predicate, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (await browser.ExecuteScriptAsync(predicate, deadline.Token) != "true") await Task.Delay(20, deadline.Token);
    }
    private static async Task WaitAsync(Func<bool> predicate, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(20, deadline.Token);
    }
}

internal sealed class ProbeForm : Form
{
    internal readonly WebView Browser = new() { Dock = DockStyle.Fill, IsJavaScriptEnabled = true };
    internal readonly Button NextSnapshot = new() { Dock = DockStyle.Top, Height = 40, Text = "Native MFN button: next passive Razor snapshot" };
    private readonly CancellationTokenSource lifetime = new();
    internal CancellationToken Lifetime { get; }
    internal ProbeForm(ResourceCatalog[] snapshots)
    {
        Lifetime = lifetime.Token; Text = "Razor / WebViewManager feasibility research"; Size = new Size(900, 620);
        var status = new Label { Dock = DockStyle.Bottom, Height = 55, Text = "Passive Razor renderer. Interactive Blazor remains blocked by current content/security policy." };
        int index = 0; NextSnapshot.Click += (_, _) => { index = 1 - index; Browser.NavigateToLocalContent(snapshots[index].Content); };
        Controls.Add(Browser); Controls.Add(status); Controls.Add(NextSnapshot);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !lifetime.IsCancellationRequested) { lifetime.Cancel(); lifetime.Dispose(); }
        base.Dispose(disposing);
    }
}
