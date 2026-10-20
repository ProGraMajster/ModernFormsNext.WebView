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
            int profile = Array.IndexOf(args, "--profile");
            if (profile < 0 || profile + 1 >= args.Length) throw new ArgumentException("Use Validate.ps1 with an owned profile.");
            WindowsPlatformBootstrap.Initialize();
            var provider = new ResearchProvider(args[profile + 1]);
            using var form = new Form { Size = new Size(900, 620), Text = "Closed program counter research; general Hybrid NO-GO" };
            using var browser = new WebView(provider) { Dock = DockStyle.Fill, IsJavaScriptEnabled = true };
            form.Controls.Add(browser);
            form.Shown += async (_, _) =>
            {
                try { await RunAsync(form, browser, provider); }
                catch (Exception error) { Console.WriteLine("CLOSED EXPERIMENT NO-GO: " + error); Environment.ExitCode = 1; }
                finally { form.Close(); }
            };
            Application.Run(form);
        }
        catch (Exception error) { Console.WriteLine("CLOSED EXPERIMENT NO-GO: " + error); Environment.ExitCode = 1; }
    }
    private static async Task RunAsync(Form form, WebView browser, ResearchProvider provider)
    {
        await WaitAsync(() => browser.IsInitialized);
        using var transport = new WebViewTransport(browser);
        var counter = new OwnedCounter();
        await NavigateAsync(browser, () => browser.NavigateToHtml(TestContent.Html));
        Console.WriteLine("ENGINE " + browser.GetDiagnostics().EngineVersion);
        Require(!browser.BrowserCapabilities.HasFlag(WebViewCapabilities.AuthenticatedMessaging), "product authentication unavailable");
        int rawPackets = 0;
        var forgedPayloads = new HashSet<string>(StringComparer.Ordinal);
        browser.WebMessageReceived += (_, e) =>
        {
            Require(!e.IsOriginAuthenticated, "all raw page/forged packets remain untrusted"); rawPackets = Math.Min(256, rawPackets + 1);
            // Evidence collector only. This untrusted payload is never forwarded to
            // ClosedSession, the native event receiver, a permit or an operation.
            if (!e.Message.StartsWith('{')) return;
            using var json = JsonDocument.Parse(e.Message);
            if (json.RootElement.TryGetProperty("name", out var name) && name.GetString() == ClosedSession.Binding &&
                json.RootElement.TryGetProperty("payload", out var payload) && payload.GetString() is { } value && forgedPayloads.Count < 16)
                forgedPayloads.Add(value);
        };
        await ScriptWaitAsync(browser, "closedReady.includes('same')&&closedReady.includes('opaque')");
        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            await PositiveAsync(session, counter, "initial native intent");
            foreach (string actor in new[] { "main", "same", "opaque" })
            {
                // Native harness already selected the fixed operation before invoking
                // the hostile actor. Deliberately disclose ALL payload/name/context data.
                string payload = session.GrantCounterIncrement();
                int before = counter.Value, calls = session.LiveCalls, raw = rawPackets;
                await browser.ExecuteScriptAsync("runClosedActor(" + JsonSerializer.Serialize(actor) + "," +
                    JsonSerializer.Serialize(new { name = ClosedSession.Binding, payload, contextId = session.ContextId }) + ");null");
                await ScriptWaitAsync(browser, "closedProof.some(p=>p.actor==='" + actor + "'&&p.steps===8&&p.direct==='undefined')");
                await WaitAsync(() => rawPackets > raw && forgedPayloads.Contains(payload));
                if (actor == "opaque") await ScriptWaitAsync(browser, "closedProof.some(p=>p.actor==='opaque'&&p.parentDenied===true)");
                if (actor == "same") await ScriptWaitAsync(browser, "closedProof.some(p=>p.actor==='same'&&!p.parentDenied)");
                // A genuine same-world native callback fences the previous attempts,
                // but uses a different payload and cannot consume the valid permit.
                await LiveDeniedAsync(session, counter, "actor-fence", "WrongOperationOrArguments");
                Require(counter.Value == before && session.LiveCalls == calls + 1, "executed " + actor + " actor never entered the isolated binding");
                Console.WriteLine("PASS parsed actor=" + actor + " direct/parent/helper/DOM/event/postMessage/prototype/named-property/forged attempts; zero unauthorized effect");
                await PositiveAsync(session, counter, "fresh control after " + actor);
            }
            string child = await session.CreateChildWorldAsync();
            string allowed = session.GrantCounterIncrement();
            await LiveDeniedAsync(session, counter, allowed, "WrongNativeContext", child);
            await PositiveAsync(session, counter, "fresh control after live same-name child world");

            foreach (string mutation in new[] { "method", "delta", "nonce" })
            {
                string exact = session.GrantCounterIncrement();
                string wrong = mutation == "method" ? exact.Replace("IncrementOwnedCounter", "ReadSecret", StringComparison.Ordinal) :
                    mutation == "delta" ? exact.Replace("delta=1", "delta=2", StringComparison.Ordinal) : exact + "wrong";
                await LiveDeniedAsync(session, counter, wrong, "WrongOperationOrArguments");
                await PositiveAsync(session, counter, "fresh control after wrong " + mutation);
            }
            string expired = session.GrantCounterIncrement(shortExpiry: true);
            await Task.Delay(80);
            await LiveDeniedAsync(session, counter, expired, "Expired");
            await PositiveAsync(session, counter, "fresh control after monotonic expiry");
            string consumed = await PositiveAsync(session, counter, "one-use control");
            await LiveDeniedAsync(session, counter, consumed, "NoPermit");
            await PositiveAsync(session, counter, "fresh control after consumed-permit replay");
            session.GrantCounterIncrement();
            await LiveDeniedAsync(session, counter, consumed, "WrongOperationOrArguments");
            await PositiveAsync(session, counter, "fresh control after live replay");
            string recorded = session.LastPacket!;
            int beforeRecorded = counter.Value;
            Require(session.InspectRecorded(recorded) == "NoPermit" && counter.Value == beforeRecorded, "recorded replay has no mutation entry");

            string stopped = session.GrantCounterIncrement();
            browser.Stop();
            Require(session.Document.CancellationToken.IsCancellationRequested, "Stop synchronously revokes canonical document");
            await LiveDeniedAsync(session, counter, stopped, "CanonicalCanceled");
            Require(session.InspectRecorded(recorded) == "CanonicalCanceled", "Stop recorded inspection distinct from live rejection");
        }
        await NavigateAsync(browser, () => Require(browser.Reload(), "reload after Stop"));
        await FreshControlAsync(provider, transport, counter, "after Stop recovery");

        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            string payload = session.GrantCounterIncrement();
            browser.IsJavaScriptEnabled = false;
            await StaleSdkAttemptAsync(session, counter, payload, "JS disable");
            browser.IsJavaScriptEnabled = true;
            await StaleSdkAttemptAsync(session, counter, payload, "JS re-enable never restores old permit");
        }
        await NavigateAsync(browser, () => Require(browser.Reload(), "reload after JS re-enable"));
        await FreshControlAsync(provider, transport, counter, "after JS lifecycle");

        await using var server = new LoopbackContentServer();
        await NavigateAsync(browser, () => browser.Navigate(server.Address("closed/start")));
        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            string payload = session.GrantCounterIncrement();
            int redirects = 0;
            EventHandler<WebViewNavigationStartingEventArgs> onStart = (_, e) => { if (e.IsRedirect) redirects++; };
            browser.NavigationStarting += onStart;
            try { await NavigateAsync(browser, () => browser.Navigate(server.Address("security/redirect"))); }
            finally { browser.NavigationStarting -= onStart; }
            Require(redirects > 0 && browser.Source?.Host == "localhost", "actual cross-origin HTTP302");
            await StaleSdkAttemptAsync(session, counter, payload, "cross-origin302");
        }
        await FreshControlAsync(provider, transport, counter, "after cross-origin302");
        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            string payload = session.GrantCounterIncrement();
            await NavigateAsync(browser, () => Require(browser.Reload(), "same URI reload"));
            await StaleSdkAttemptAsync(session, counter, payload, "same URI reload");
        }
        await FreshControlAsync(provider, transport, counter, "after reload");

        using var second = new Form { Size = new Size(900, 620), Text = "Closed program actual peer recreation" };
        second.Show();
        int oldId; string oldUnique;
        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            string payload = session.GrantCounterIncrement(); oldId = session.ContextId; oldUnique = session.ContextUniqueId;
            var oldPeer = provider.Peer;
            await NavigateAsync(browser, () => second.Controls.Add(browser));
            Require(!oldPeer.Current && transport.CurrentDocument.PeerGeneration > session.Document.PeerGeneration, "actual native peer replacement");
            await StaleSdkAttemptAsync(session, counter, payload, "cross-window peer recreation");
        }
        await using (var fresh = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            Require(fresh.ContextUniqueId != oldUnique, "native unique identity differs across actual peers");
            Console.WriteLine($"CROSS PEER oldNumeric={oldId} freshNumeric={fresh.ContextId}; numeric collision is not assumed; exact peer/channel and unique identity are required");
            await PositiveAsync(fresh, counter, "after peer recreation");
        }
        await NavigateAsync(browser, () => form.Controls.Add(browser));
        await FreshControlAsync(provider, transport, counter, "after return host recreation");
        await using (var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter))
        {
            string payload = session.GrantCounterIncrement(); browser.Dispose();
            await StaleSdkAttemptAsync(session, counter, payload, "Dispose");
        }
        using var replacement = new WebView(provider) { Dock = DockStyle.Fill, IsJavaScriptEnabled = true };
        form.Controls.Add(replacement); await WaitAsync(() => replacement.IsInitialized);
        using var freshTransport = new WebViewTransport(replacement);
        await NavigateAsync(replacement, () => replacement.NavigateToHtml(TestContent.Html));
        await FreshControlAsync(provider, freshTransport, counter, "after disposed control replacement");
        Console.WriteLine($"CLOSED EXPERIMENT EXECUTED; NARROW DIAGNOSTIC GO counter={counter.Value}; GENERAL HYBRID / BLAZOR ATTACHPAGE NO-GO; PRODUCT AUTHENTICATION UNCHANGED");
    }
    private static async Task<string> PositiveAsync(ClosedSession session, OwnedCounter counter, string label)
    {
        int before = counter.Value, calls = session.LiveCalls;
        string payload = session.GrantCounterIncrement(); await session.EvaluatePayloadAsync(payload);
        await WaitAsync(() => session.LiveCalls > calls);
        Require(session.LastReason == "Accepted" && counter.Value == before + 1, label);
        Console.WriteLine("PASS fresh native authorized control: " + label); return payload;
    }
    private static async Task FreshControlAsync(ResearchProvider provider, IWebViewTransport transport, OwnedCounter counter, string label)
    { await using var session = await ClosedSession.CreateAsync(provider.Peer, transport, counter); await PositiveAsync(session, counter, label); }
    private static async Task LiveDeniedAsync(ClosedSession session, OwnedCounter counter, string payload, string reason, string? context = null)
    {
        int before = counter.Value, calls = session.LiveCalls;
        await session.EvaluatePayloadAsync(payload, context); await WaitAsync(() => session.LiveCalls > calls);
        Require(counter.Value == before && session.LastReason == reason, "actual native diagnostic rejection: " + reason);
    }
    private static async Task StaleSdkAttemptAsync(ClosedSession session, OwnedCounter counter, string payload, string label)
    {
        Require(session.Document.CancellationToken.IsCancellationRequested, "canonical revocation before " + label);
        int before = counter.Value, calls = session.LiveCalls;
        string packet = session.LastPacket!;
        bool refused = false;
        try { await session.EvaluatePayloadAsync(payload); }
        catch (ArgumentException) { refused = true; }
        catch (System.Runtime.InteropServices.COMException) { refused = true; }
        catch (InvalidOperationException) { refused = true; }
        if (!refused) await WaitAsync(() => session.LiveCalls > calls);
        Require(counter.Value == before && (refused || session.LastReason == "CanonicalCanceled"), "stale SDK attempt no effect: " + label);
        Require(session.InspectRecorded(packet) == "CanonicalCanceled", "recorded canceled guard: " + label);
        Console.WriteLine("PASS " + label + (refused ? ": actual SDK execution REFUSED; no live callback claimed" : ": actual SDK callback received and rejected"));
    }
    internal static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static async Task WaitAsync(Func<bool> predicate)
    { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); while (!predicate()) await Task.Delay(20, timeout.Token); }
    private static async Task ScriptWaitAsync(WebView browser, string predicate)
    { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); while (await browser.ExecuteScriptAsync(predicate, timeout.Token) != "true") await Task.Delay(20, timeout.Token); }
    private static async Task NavigateAsync(WebView browser, Action action)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<WebViewNavigationCompletedEventArgs> done = (_, _) => completed.TrySetResult();
        EventHandler<WebViewNavigationFailedEventArgs> failed = (_, e) => completed.TrySetException(new InvalidOperationException("Native navigation failed: " + e.Category));
        browser.NavigationCompleted += done; browser.NavigationFailed += failed;
        try { action(); await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { browser.NavigationCompleted -= done; browser.NavigationFailed -= failed; }
    }
}
