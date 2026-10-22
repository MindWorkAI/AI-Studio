using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// A minimal HTTP server on the loopback address, which stands in for the intranet in tests: it
/// is the one non-public address a test can serve from.
/// </summary>
/// <remarks>
/// /redirect sends the client on, to /page of this server unless the test names another address,
/// /document answers with the document the test hands in, and every other path answers with a
/// small HTML page.
/// </remarks>
internal sealed class LoopbackWebServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly string? redirectLocation;
    private readonly LoopbackDocument? document;

    /// <param name="redirectLocation">Where /redirect sends the client, or null for /page of this server.</param>
    /// <param name="document">What /document answers with, or null for the HTML page.</param>
    public LoopbackWebServer(string? redirectLocation = null, LoopbackDocument? document = null)
    {
        this.redirectLocation = redirectLocation;
        this.document = document;
        this.listener.Start();
        _ = this.ServeAsync();
    }

    public int Port => ((IPEndPoint)this.listener.LocalEndpoint).Port;

    public Uri Address(string path) => new($"http://127.0.0.1:{this.Port}{path}");

    private async Task ServeAsync()
    {
        try
        {
            while (!this.stop.IsCancellationRequested)
            {
                using var client = await this.listener.AcceptTcpClientAsync(this.stop.Token);
                await using var stream = client.GetStream();
                var request = await ReadRequestHeadAsync(stream, this.stop.Token);
                if (this.document is { } served && request.StartsWith("GET /document ", StringComparison.Ordinal))
                {
                    var disposition = served.FileName is null ? string.Empty : $"Content-Disposition: attachment; filename=\"{served.FileName}\"\r\n";
                    var head = $"HTTP/1.1 200 OK\r\nContent-Type: {served.ContentType}\r\nContent-Length: {served.Body.Length}\r\n{disposition}Connection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head), this.stop.Token);
                    await stream.WriteAsync(served.Body, this.stop.Token);
                    continue;
                }

                var response = request.StartsWith("GET /redirect ", StringComparison.Ordinal)
                    ? $"HTTP/1.1 302 Found\r\nLocation: {this.redirectLocation ?? this.Address("/page").ToString()}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : Page("<html><head><title>Travel</title></head><body><main><p>The travel policy of the intranet.</p></main></body></html>");

                await stream.WriteAsync(Encoding.UTF8.GetBytes(response), this.stop.Token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // The test is over.
        }
    }

    private static string Page(string html) => $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\n\r\n{html}";

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

/// <summary>
/// A document the LoopbackWebServer serves at /document.
/// </summary>
/// <param name="ContentType">The media type the server claims.</param>
/// <param name="Body">The bytes of the document.</param>
/// <param name="FileName">The file name of the Content-Disposition header, or null for none.</param>
internal sealed record LoopbackDocument(string ContentType, byte[] Body, string? FileName = null);