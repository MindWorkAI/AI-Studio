using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Plays the image route of the runtime for a test, and notes what the app asked for.
/// </summary>
/// <remarks>
/// It prepares nothing: every image comes back as the same three bytes, with the code and type the
/// test sets. The answer is spelled out in the wire format of the runtime, so a test fails as soon
/// as the app stops understanding what the runtime actually sends.
/// </remarks>
internal sealed class FakeImageRuntime : HttpMessageHandler
{
    /// <summary>
    /// The three bytes every prepared image consists of, as base64.
    /// </summary>
    public const string IMAGE_BASE64 = "AQID";

    private readonly ConcurrentQueue<string> requestedPaths = new();
    private readonly ConcurrentQueue<long> requestedBudgets = new();

    /// <summary>
    /// The code the runtime answers with, as the runtime spells it.
    /// </summary>
    public string ErrorCode { get; set; } = "NONE";

    /// <summary>
    /// The MIME type the runtime reports for every prepared image.
    /// </summary>
    public string MimeType { get; set; } = "image/jpeg";

    /// <summary>
    /// The paths of the images the app asked to prepare, in order.
    /// </summary>
    public IReadOnlyList<string> RequestedPaths => [..this.requestedPaths];

    /// <summary>
    /// The byte budgets the app asked for, in order.
    /// </summary>
    public IReadOnlyList<long> RequestedBudgets => [..this.requestedBudgets];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath is not "/image/prepare-for-model")
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        using (var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)))
        {
            this.requestedPaths.Enqueue(body.RootElement.GetProperty("path").GetString() ?? string.Empty);
            this.requestedBudgets.Enqueue(body.RootElement.GetProperty("max_bytes").GetInt64());
        }

        var answer = JsonSerializer.Serialize(new { error_code = this.ErrorCode, base64 = IMAGE_BASE64, mime_type = this.MimeType, width = 1, height = 1, was_resized = false });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }
}