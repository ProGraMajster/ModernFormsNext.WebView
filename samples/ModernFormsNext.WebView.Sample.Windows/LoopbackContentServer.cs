using System.Net;
using System.Net.Sockets;
using System.Text;
using ModernFormsNext.WebView.Sample;

internal sealed class LoopbackContentServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Task serving;
    private readonly SemaphoreSlim connections = new(16);
    internal LoopbackContentServer() { listener.Start(); serving = ServeAsync(); }
    internal Uri Address(string path) => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{path}");
    private async Task ServeAsync()
    {
        var requests = new List<Task>();
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var connection = await listener.AcceptTcpClientAsync(stopping.Token);
                try { await connections.WaitAsync(stopping.Token); }
                catch { connection.Dispose(); throw; }
                requests.RemoveAll(task => task.IsCompletedSuccessfully);
                requests.Add(RespondAsync(connection));
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (SocketException) when (stopping.IsCancellationRequested) { }
        finally { await Task.WhenAll(requests); }
    }
    private async Task RespondAsync(TcpClient connection)
    {
        using (connection)
        using (var requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token))
        {
            // Chromium can leave a speculative connection idle. It must not prevent
            // another origin's redirect/frame request from being accepted by the fixture.
            requestLifetime.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var stream = connection.GetStream();
                var buffer = new byte[8192];
                int length = await stream.ReadAsync(buffer, requestLifetime.Token);
                if (length == 0) return;
                string requestLine = Encoding.ASCII.GetString(buffer, 0, length).Split('\n')[0];
                string path = requestLine.Split(' ').ElementAtOrDefault(1) ?? "/";
                bool redirect = path == "/security/redirect";
                string html = path.StartsWith("/security/frame?", StringComparison.Ordinal)
                    ? "<script>const id=Number(new URL(location.href).searchParams.get('id'));chrome.webview.postMessage(JSON.stringify([id,'security-foreign']));parent.postMessage('security-frame-done','*');</script>"
                    : TestContent.Html;
                byte[] body = redirect ? [] : Encoding.UTF8.GetBytes(html);
                string status = redirect ? "302 Found" : "200 OK";
                string location = redirect ? "Location: " + Address("security/top").AbsoluteUri.Replace("127.0.0.1", "localhost", StringComparison.Ordinal) + "\r\n" : "";
                byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\n{location}Content-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, requestLifetime.Token); await stream.WriteAsync(body, requestLifetime.Token);
            }
            catch (OperationCanceledException) when (requestLifetime.IsCancellationRequested) { }
            catch (IOException) { /* A canceled browser request closes its test-owned connection. */ }
            finally { connections.Release(); }
        }
    }
    public async ValueTask DisposeAsync()
    {
        stopping.Cancel(); listener.Stop();
        try { await serving; } finally { stopping.Dispose(); connections.Dispose(); }
    }
}
