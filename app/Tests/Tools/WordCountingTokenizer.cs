using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Plays the tokenizer of the runtime for tests which cut texts into chunks: one token per word.
/// </summary>
/// <remarks>
/// Counting words is all it takes to make a chunk full, so a test can say exactly where a chunk ends.
/// </remarks>
internal sealed class WordCountingTokenizer : HttpMessageHandler
{
    public static int Count(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var text = body.RootElement.GetProperty("text").GetString() ?? string.Empty;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, token_count = Count(text), message = string.Empty }) };
    }
}