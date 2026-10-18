using System.Text.Json.Nodes;
using ModernFormsNext.Testing;

namespace ModernFormsNext.WebView.Tests;

public sealed class RpcLifetimeIntegrationTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly ModernFormsTestHost App = ModernFormsTestHost.Create();
        internal readonly Form Form = new();
        internal readonly FakeProvider Provider = new();
        internal readonly WebView Browser;
        internal readonly TestWindowHost Window;
        internal FakePeer Peer => Provider.Peers.Last();
        internal Fixture()
        {
            Browser = Form.Controls.Add(new WebView(Provider) { Bounds = new(0, 0, 100, 100), IsJavaScriptEnabled = true });
            Window = App.Show(Form); Peer.Ready(); Browser.NavigateToHtml("document");
            Peer.Client.NavigationCompleted(Peer.Id, null, 200);
        }
        public void Dispose() { Window.Dispose(); Form.Dispose(); App.Dispose(); }
    }
    private static Task<int> Invoke(WebViewRpcClient rpc) => rpc.InvokeAsync("echo", 1, RpcTestJson.Default.Int32, RpcTestJson.Default.Int32);
    private static string Reply(string wire) { var r = JsonNode.Parse(wire)!; return new JsonObject
    { ["version"] = 1, ["type"] = "result", ["id"] = r["id"]!.GetValue<string>(), ["document"] = r["document"]!.GetValue<string>(), ["payload"] = 42 }.ToJsonString(); }

    [Theory]
    [InlineData("navigation")]
    [InlineData("stop")]
    [InlineData("script-disable")]
    [InlineData("host-loss")]
    [InlineData("browser")]
    [InlineData("transport")]
    [InlineData("client")]
    public async Task CanonicalOwnerInvalidationCancelsRpcThroughRealTransport(string boundary)
    {
        Task<int> pending;
        using (var f = new Fixture())
        {
            using var transport = new WebViewTransport(f.Browser); using var rpc = new WebViewRpcClient(transport);
            pending = Invoke(rpc);
            switch (boundary)
            {
                case "navigation": f.Browser.NavigateToHtml("replacement"); break;
                case "stop": f.Browser.Stop(); break;
                case "script-disable": f.Browser.IsJavaScriptEnabled = false; break;
                case "host-loss": f.Window.NativeViews.SetAvailable(false); break;
                case "browser": f.Browser.Dispose(); break;
                case "transport": transport.Dispose(); break;
                default: rpc.Dispose(); break;
            }
        }
        // Headless hosts must be released on their owning thread before xUnit awaits.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task RetiredNativeHostCannotCompleteCallAfterReinitialization()
    {
        Task<int> obsolete, current;
        using (var f = new Fixture())
        {
            using var transport = new WebViewTransport(f.Browser); using var rpc = new WebViewRpcClient(transport);
            var oldPeer = f.Peer; obsolete = Invoke(rpc); string oldReply = Reply(oldPeer.Message!);
            long oldGeneration = transport.CurrentDocument.PeerGeneration;
            f.Window.NativeViews.SetAvailable(false); f.Window.NativeViews.SetAvailable(true);
            f.Peer.Ready(); f.Peer.Client.NavigationCompleted(f.Peer.Id, null, 200);
            Assert.True(transport.CurrentDocument.IsReady); Assert.True(transport.CurrentDocument.PeerGeneration > oldGeneration);
            current = Invoke(rpc);
            oldPeer.Client.WebMessageReceived(oldPeer.Id, oldReply, null); Assert.False(current.IsCompleted);
            f.Peer.Client.WebMessageReceived(f.Peer.Id, Reply(f.Peer.Message!), null);
            // Keep this canonical host alive and drain its dispatcher until the detached
            // completion settles. Awaiting under xUnit can switch the OS thread; disposing
            // first instead correctly cancels the new call before conversion can finish.
            var completionDeadline = System.Diagnostics.Stopwatch.StartNew();
            while (!current.IsCompleted && completionDeadline.Elapsed < TimeSpan.FromSeconds(5))
            { f.App.ProcessPendingWork(); Thread.Yield(); }
            Assert.True(current.IsCompleted);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => obsolete); Assert.Equal(42, await current);
    }
}
