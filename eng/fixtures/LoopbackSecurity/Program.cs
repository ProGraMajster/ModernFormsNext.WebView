using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Test-owned loopback only. It never installs a trust root or supplies a client-side
// certificate-validation callback. A real browser must reject its untrusted certificate.
internal static class Program
{
    private static readonly ConcurrentDictionary<string, int> Requests = new(StringComparer.Ordinal);
    private static int tlsConnections;
    private static int failures;
    private static async Task Main()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; lifetime.Cancel(); };
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        // Windows Schannel cannot authenticate this server with an ephemeral private
        // key. Import a temporary default-key-set key; Dispose removes it. No certificate
        // enters a trust store and PersistKeySet is deliberately absent.
        // https://learn.microsoft.com/dotnet/core/extensions/sslstream-troubleshooting#handshake-failed-with-ephemeral-keys
        byte[] pfx = generated.Export(X509ContentType.Pkcs12);
        using var certificate = X509CertificateLoader.LoadPkcs12(pfx, null,
            OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
        CryptographicOperations.ZeroMemory(pfx);
        using var http = new TcpListener(IPAddress.Loopback, 0);
        using var tls = new TcpListener(IPAddress.Loopback, 0);
        http.Start(); tls.Start();
        string httpUri = "http://127.0.0.1:" + ((IPEndPoint)http.LocalEndpoint).Port;
        string tlsUri = "https://127.0.0.1:" + ((IPEndPoint)tls.LocalEndpoint).Port;
        Console.WriteLine(JsonSerializer.Serialize(new { httpUri, tlsUri, processId = Environment.ProcessId, certificateSHA256 = certificate.GetCertHashString(HashAlgorithmName.SHA256), lifetimeSeconds = 180 }));
        Console.Out.Flush();
        try { await Task.WhenAll(AcceptAsync(http, null, lifetime.Token), AcceptAsync(tls, certificate, lifetime.Token), StopAsync(lifetime)); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    private static async Task StopAsync(CancellationTokenSource lifetime)
    {
        // The owning driver has the only redirected stdin handle. Graceful cancellation
        // lets using scopes dispose the Schannel temporary private key before exit.
        string? command = await Console.In.ReadLineAsync(lifetime.Token);
        if (command == "stop") lifetime.Cancel();
        else await Task.Delay(Timeout.Infinite, lifetime.Token);
    }

    private static async Task AcceptAsync(TcpListener listener, X509Certificate2? certificate, CancellationToken token)
    {
        // At most one bounded request per connection; sequential serving bounds state,
        // sockets and logs. This is an offline fixture, never an application web server.
        while (!token.IsCancellationRequested)
        {
            using var client = await listener.AcceptTcpClientAsync(token);
            using var requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            requestLifetime.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var network = client.GetStream();
                if (certificate is null) await ServeAsync(network, requestLifetime.Token);
                else
                {
                    Interlocked.Increment(ref tlsConnections);
                    using var secure = new SslStream(network, leaveInnerStreamOpen: true);
                    await secure.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate, ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, requestLifetime.Token);
                    await ServeAsync(secure, requestLifetime.Token);
                }
            }
            catch (AuthenticationException error) { Failure(error); } // Also diagnose a broken fixture, not just expected browser rejection.
            catch (IOException error) { Failure(error); } // A rejecting browser may close before TLS/HTTP completes.
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        }
    }

    private static void Failure(Exception error)
    {
        if (Interlocked.Increment(ref failures) <= 8) Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
    }

    private static async Task ServeAsync(Stream stream, CancellationToken token)
    {
        byte[] buffer = new byte[8192];
        int length = 0;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(length), token);
            if (read == 0) return;
            length += read;
            if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
        }
        if (length == 0) return;
        string firstLine = Encoding.ASCII.GetString(buffer, 0, length).Split('\n')[0];
        string path = firstLine.Split(' ').ElementAtOrDefault(1)?.Split('?')[0] ?? "/";
        if (Requests.Count < 16 || Requests.ContainsKey(path)) Requests.AddOrUpdate(path, 1, (_, value) => Math.Min(1000, value + 1));
        string mime = path == "/state" ? "application/json" : "text/plain";
        byte[] body = Encoding.UTF8.GetBytes(path == "/state" ? JsonSerializer.Serialize(new { tlsConnections, requests = Requests }) : "owned security fixture");
        bool attachment = Regex.IsMatch(path, "^/download-[0-9a-f]{32}\\.txt$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        string disposition = attachment ? "Content-Disposition: attachment; filename=\"" + path[1..] + "\"\r\n" : "";
        string headers = "HTTP/1.1 200 OK\r\nContent-Type: " + mime + "\r\n" + disposition +
            "Content-Length: " + body.Length + "\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers), token); await stream.WriteAsync(body, token);
    }
}
