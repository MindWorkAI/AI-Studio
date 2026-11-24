using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AIStudio.Tests.Tools;

/// <summary>
/// A minimal ERI server on the loopback address, which signs every client in with the session token
/// the test names.
/// </summary>
/// <remarks>
/// POST /auth answers with that token, and every other request with an error, so that a test sees what
/// the client does with the token before it would need anything else of the server.
/// </remarks>
internal sealed class LoopbackERIServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly string sessionToken;

    /// <param name="sessionToken">The session token the server hands out.</param>
    public LoopbackERIServer(string sessionToken)
    {
        this.sessionToken = sessionToken;
        this.listener.Start();
        _ = this.ServeAsync();
    }

    public int Port => ((IPEndPoint)this.listener.LocalEndpoint).Port;

    private async Task ServeAsync()
    {
        try
        {
            while (!this.stop.IsCancellationRequested)
            {
                using var client = await this.listener.AcceptTcpClientAsync(this.stop.Token);
                await using var stream = client.GetStream();
                var request = await ReadRequestHeadAsync(stream, this.stop.Token);
                var response = request.StartsWith("POST /auth", StringComparison.Ordinal)
                    ? Json($$"""{"success":true,"token":"{{JsonEncodedText.Encode(this.sessionToken)}}","message":null}""")
                    : "HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

                await stream.WriteAsync(Encoding.UTF8.GetBytes(response), this.stop.Token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // The test is over.
        }
    }

    private static string Json(string json) => $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\nConnection: close\r\n\r\n{json}";

    private static async Task<string> ReadRequestHeadAsync(NetworkStream stream, CancellationToken token)
    {
        var head = new StringBuilder();
        var buffer = new byte[1024];
        while (!head.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, token);
            if (read == 0)
                break;

            head.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        return head.ToString();
    }

    public void Dispose()
    {
        this.stop.Cancel();
        this.listener.Stop();
        this.stop.Dispose();
    }
}