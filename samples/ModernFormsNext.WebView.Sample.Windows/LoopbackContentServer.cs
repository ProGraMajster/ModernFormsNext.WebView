using System.Net;
using System.Net.Sockets;
using System.Text;
using ModernFormsNext.WebView.Sample;

internal sealed class LoopbackContentServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Task serving;
    internal LoopbackContentServer() { listener.Start(); serving = ServeAsync(); }
    internal Uri Address(string path) => new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/{path}");
    private async Task ServeAsync()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                using var connection = await listener.AcceptTcpClientAsync(stopping.Token);
                using var stream = connection.GetStream();
                var buffer = new byte[8192];
                int length = await stream.ReadAsync(buffer, stopping.Token);
                if (length == 0) continue;
                byte[] body = Encoding.UTF8.GetBytes(TestContent.Html);
                byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                try { await stream.WriteAsync(header, stopping.Token); await stream.WriteAsync(body, stopping.Token); }
                catch (IOException) { /* A canceled browser request closes its test-owned connection. */ }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        catch (SocketException) when (stopping.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        stopping.Cancel(); listener.Stop();
        try { await serving; } finally { stopping.Dispose(); }
    }
}
