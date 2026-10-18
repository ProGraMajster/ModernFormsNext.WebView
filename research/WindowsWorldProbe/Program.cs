using System.Drawing;
using System.Text.Json;
using ModernFormsNext.WebView.Sample;
using ModernFormsNext.WindowKit.Backend.Windows;

namespace ModernFormsNext.WebView.WorldProbe;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            int profileArg = Array.IndexOf(args, "--profile");
            if (profileArg < 0 || profileArg + 1 >= args.Length) throw new ArgumentException("Use Validate.ps1 with an owned profile.");
            WindowsPlatformBootstrap.Initialize();
            var provider = new ResearchProvider(args[profileArg + 1]);
            using var form = new Form { Size = new Size(900, 620), Text = "Diagnostic isolated-world research; privileged NO-GO" };
            using var browser = new WebView(provider) { Dock = DockStyle.Fill, IsJavaScriptEnabled = true };
            form.Controls.Add(browser);
            form.Shown += async (_, _) =>
            {
                try { await RunAsync(form, browser, provider); }
                catch (Exception error) { Console.WriteLine("WORLD FAIL: " + error); Environment.ExitCode = 1; }
                finally { form.Close(); }
            };
            Application.Run(form); form.Dispose();
        }
        catch (Exception error) { Console.WriteLine("WORLD FAIL: " + error); Environment.ExitCode = 1; }
    }
    private static async Task RunAsync(Form form, WebView browser, ResearchProvider provider)
    {
        await WaitAsync(() => browser.IsInitialized);
        using var transport = new WebViewTransport(browser);
        await NavigateAsync(browser, () => browser.NavigateToHtml(TestContent.Html));
        Console.WriteLine("ENGINE " + browser.GetDiagnostics().EngineVersion);
        Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "no authentication capability");
        int untrusted = 0;
        browser.WebMessageReceived += (_, e) => { Require(!e.IsOriginAuthenticated, "raw page remains untrusted"); untrusted = Math.Min(256, untrusted + 1); };
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            // Actual unsupported method response, with no global/main-world fallback.
            bool unsupported = false;
            try { await session.CallAsync("MFNUnsupportedWorldProbe.noSuchMethod", new { }); }
            catch (System.Runtime.InteropServices.COMException) { unsupported = true; }
            catch (ArgumentException) { unsupported = true; } // WebView2 projects E_INVALIDARG here.
            Require(unsupported, "unsupported feature fails explicitly");
            int before = session.Accepted;
            await session.EvaluateAsync("globalThis.worldPrivate=42;document.addEventListener('world-command',()=>{" + session.Name + "(document.documentElement.dataset.command)});");
            await browser.ExecuteScriptAsync("attackWorld(" + JsonSerializer.Serialize(session.Name) + ");null");
            await WaitAsync(() => session.Payloads.Contains("page-dom") && session.Payloads.Contains("iframe-dom") && untrusted >= 3);
            await WaitScriptAsync(browser, "proof.some(x=>x.actor==='opaque'&&x.parentDomDenied&&x.direct==='undefined')&&proof.some(x=>x.actor==='same'&&x.direct==='undefined'&&x.parentDirect==='undefined')");
            Require(await browser.ExecuteScriptAsync("typeof worldPrivate") == "\"undefined\"", "world global isolated from page");
            Require(await browser.ExecuteScriptAsync("proof.find(x=>x.actor==='page').direct") == "\"undefined\"", "page binding absent");
            Require(session.Accepted > before, "native world context accepted DOM-controlled data in diagnostic counter");
            Require(!session.Payloads.Contains("forged"), "page JSON never reaches native world event channel");
            Console.WriteLine("PASS isolated native context + page/same-origin/opaque iframe binding absence + executed parent relay + forged native packet remains untrusted");
            Console.WriteLine("COUNTEREXAMPLE PASS: page and iframe DOM commands enter genuine isolated-world binding; context identity is not operation authorization");
            string oldPacket = session.LastPacket!;
            int oldAccepted = session.Accepted;
            browser.Stop();
            session.Observe(oldPacket);
            // Native CDP can execute after Stop, but the canonical revoked token must
            // reject its real callback. No second lifecycle authority is introduced.
            await session.EvaluateAsync(session.Name + "('after-stop')");
            await WaitAsync(() => session.Rejected >= 2);
            Require(session.Accepted == oldAccepted, "Stop rejects recorded and live native binding calls");
            Console.WriteLine("PASS canonical Stop cancellation rejects live world callback and recorded packet");
        }
        await NavigateAsync(browser, () => Require(browser.Reload(), "raw reload"));
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            int accepted = session.Accepted;
            browser.IsJavaScriptEnabled = false;
            session.Observe(session.LastPacket!);
            Require(session.Accepted == accepted && session.Rejected > 0, "JS disable revokes canonical world guard");
            browser.IsJavaScriptEnabled = true;
            session.Observe(session.LastPacket!);
            Require(session.Accepted == accepted, "JS re-enable does not restore old authority");
        }
        await NavigateAsync(browser, () => Require(browser.Reload(), "enabled reload"));
        await using var server = new LoopbackContentServer();
        await NavigateAsync(browser, () => browser.Navigate(server.Address("world/start")));
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            string packet = session.LastPacket!; int accepted = session.Accepted;
            var oldDocument = transport.CurrentDocument;
            int redirects = 0;
            EventHandler<WebViewNavigationStartingEventArgs> starting = (_, e) => { if (e.IsRedirect) redirects++; };
            browser.NavigationStarting += starting;
            try { await NavigateAsync(browser, () => browser.Navigate(server.Address("security/redirect"))); }
            finally { browser.NavigationStarting -= starting; }
            session.Observe(packet);
            Require(redirects > 0 && browser.Source?.Host == "localhost" && oldDocument.CancellationToken.IsCancellationRequested &&
                session.Accepted == accepted && session.Rejected > 0, "real cross-origin 302 revokes prior world document");
            Require(await browser.ExecuteScriptAsync("typeof " + session.Name) == "\"undefined\"", "binding absent after redirect");
            bool oldBindingUnavailable = false;
            try { await session.EvaluateAsync(session.Name + "('after-redirect')"); }
            catch (ArgumentException) { oldBindingUnavailable = true; }
            catch (System.Runtime.InteropServices.COMException) { oldBindingUnavailable = true; }
            catch (InvalidOperationException) { oldBindingUnavailable = true; }
            Require(oldBindingUnavailable && session.Accepted == accepted, "old native world binding cannot execute after redirect");
            Console.WriteLine("PASS actual HTTP302 origin change + canonical cancellation + old packet rejection + no binding inheritance");
        }
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            string packet = session.LastPacket!; int accepted = session.Accepted;
            var old = transport.CurrentDocument;
            await NavigateAsync(browser, () => Require(browser.Reload(), "same URI reload"));
            session.Observe(packet);
            Require(old.CancellationToken.IsCancellationRequested && session.Accepted == accepted && session.Rejected > 0, "same URI reload retires old context");
            await using var fresh = await WorldSession.CreateAsync(provider.Peer, transport);
            Require(fresh.Payloads.Contains("positive"), "new document world positive control");
        }
        using var second = new Form { Size = new Size(900, 620), Text = "World probe peer recreation" };
        second.Show();
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            string packet = session.LastPacket!; int accepted = session.Accepted;
            var oldPeer = provider.Peer; var oldDocument = transport.CurrentDocument;
            await NavigateAsync(browser, () => second.Controls.Add(browser));
            session.Observe(packet);
            Require(!oldPeer.Current && oldDocument.CancellationToken.IsCancellationRequested &&
                transport.CurrentDocument.PeerGeneration > oldDocument.PeerGeneration &&
                session.Accepted == accepted && session.Rejected > 0, "real peer recreation revokes world authority");
            await using var fresh = await WorldSession.CreateAsync(provider.Peer, transport);
            Require(fresh.Payloads.Contains("positive"), "new peer world positive control");
            await NavigateAsync(browser, () => form.Controls.Add(browser));
            Console.WriteLine("PASS same URI reload + real host/peer recreation + old native context denial + fresh positive controls");
        }
        await using (var session = await WorldSession.CreateAsync(provider.Peer, transport))
        {
            string packet = session.LastPacket!; int accepted = session.Accepted;
            browser.Dispose(); session.Observe(packet);
            Require(session.Accepted == accepted && session.Rejected > 0, "Dispose denies captured context");
        }
        Console.WriteLine("WORLD EXECUTED; PUBLIC SDK ISOLATION OBSERVED; PRIVILEGED HYBRID NO-GO; ANDROID WORLDS NOT QUALIFIED");
    }
    internal static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
    internal static async Task WaitAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!predicate()) await Task.Delay(20, timeout.Token);
    }
    private static async Task WaitScriptAsync(WebView browser, string predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await browser.ExecuteScriptAsync(predicate, timeout.Token) != "true") await Task.Delay(20, timeout.Token);
    }
    private static async Task NavigateAsync(WebView browser, Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationCompletedEventArgs> completed = (_, _) => completion.TrySetResult();
        EventHandler<WebViewNavigationFailedEventArgs> failed = (_, e) => completion.TrySetException(new InvalidOperationException("Research navigation failed: " + e.Category));
        browser.NavigationCompleted += completed; browser.NavigationFailed += failed;
        try { action(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { browser.NavigationCompleted -= completed; browser.NavigationFailed -= failed; }
    }
}
