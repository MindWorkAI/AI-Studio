using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Plays the runtime for a test, so whatever goes through RustService can be checked without one.
/// </summary>
/// <remarks>
/// It reads files from the disk and filters prompt injections, like the runtime, but only as far as
/// a test needs it: the one passage the test names is replaced by the marker of the runtime wherever
/// it occurs, in a file as well as in a text. The answers are spelled out in the wire format of the
/// runtime, so a test fails as soon as the app stops understanding what the runtime actually sends.
/// </remarks>
/// <param name="suspiciousPassage">The passage the filter removes.</param>
internal sealed class FakeRuntime(string suspiciousPassage) : HttpMessageHandler
{
    /// <summary>
    /// What the runtime puts where it removed a passage.
    /// </summary>
    public const string REDACTION_MARKER = "[AI Studio removed suspicious content here]";

    private readonly ConcurrentQueue<string> readFiles = new();
    private readonly ConcurrentQueue<string> filteredTexts = new();

    /// <summary>
    /// The paths of the files the app asked to read, in order.
    /// </summary>
    public IReadOnlyList<string> ReadFiles => [..this.readFiles];

    /// <summary>
    /// The texts the app asked to filter, in order.
    /// </summary>
    public IReadOnlyList<string> FilteredTexts => [..this.filteredTexts];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        switch (uri.AbsolutePath)
        {
            case "/retrieval/fs/extract":
                var query = HttpUtility.ParseQueryString(uri.Query);
                var path = query["path"] ?? string.Empty;
                this.readFiles.Enqueue(path);

                var content = await File.ReadAllTextAsync(path, cancellationToken);
                return Answer(this.StreamFile(query["stream_id"] ?? string.Empty, content), "text/event-stream");

            case "/security/prompt-injection/sanitize":
                using (var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)))
                {
                    var text = body.RootElement.GetProperty("text").GetString() ?? string.Empty;
                    this.filteredTexts.Enqueue(text);

                    var filtered = this.Filter(text);
                    return Answer(JsonSerializer.Serialize(new { sanitized_text = filtered.Text, findings = filtered.Findings, redacted_count = filtered.Findings.Length }), "application/json");
                }

            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    /// The events the runtime sends for a text file: its filtered text, then what the filter removed.
    /// </summary>
    private string StreamFile(string streamId, string content)
    {
        var filtered = this.Filter(content);
        var events = new StringBuilder();
        AppendEvent(new { content = filtered.Text, stream_id = streamId, metadata = new { Text = new { line_number = 1 } } });
        if (filtered.Findings.Length > 0)
            AppendEvent(new { content = string.Empty, stream_id = streamId, metadata = new { PromptInjection = new { findings = filtered.Findings, redacted_count = filtered.Findings.Length } } });

        return events.ToString();

        void AppendEvent(object sseEvent) => events.Append("data: ").Append(JsonSerializer.Serialize(sseEvent)).Append("\n\n");
    }

    private FilteredText Filter(string text)
    {
        var occurrences = text.Split(suspiciousPassage).Length - 1;
        var findings = Enumerable.Repeat<object>(new { rule_id = "instruction_override", category = "override", snippet = suspiciousPassage }, occurrences).ToArray();
        return new(text.Replace(suspiciousPassage, REDACTION_MARKER, StringComparison.Ordinal), findings);
    }

    private static HttpResponseMessage Answer(string body, string mediaType) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    /// <summary>
    /// A text after the filter, with one finding per removed passage in the wire format of the runtime.
    /// </summary>
    private sealed record FilteredText(string Text, object[] Findings);
}