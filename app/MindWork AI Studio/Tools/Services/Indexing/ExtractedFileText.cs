using AIStudio.Settings;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Reads the text of a file through the runtime, in the pieces the chunking cuts along.
/// </summary>
/// <remarks>
/// The runtime filters prompt injections while it reads, so the text arrives filtered.
/// </remarks>
internal static class ExtractedFileText
{
    /// <summary>
    /// Reads the text of a file.
    /// </summary>
    /// <param name="rustService">The runtime.</param>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="embeddingProvider">The embedding provider whose tokenizer counts the pieces.</param>
    /// <param name="reportAs">The source the user is told about when passages were filtered out, or null for the file itself.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text, empty when the file has none.</returns>
    public static async Task<SegmentedText> ReadAsync(RustService rustService, string filePath, EmbeddingProvider embeddingProvider, PromptInjectionSource? reportAs, CancellationToken token)
    {
        var segments = new List<TextSegment>();

        await foreach (var segment in rustService.StreamArbitraryFileDataWithTokenCounts(filePath, embeddingProvider, reportAs, token))
        {
            var normalized = TextChunker.NormalizeSegment(segment.Content);
            if (!string.IsNullOrWhiteSpace(normalized))
                segments.Add(new(normalized, segment.TokenCount, segment.PageNumber));
        }

        return new(string.Join("\n", segments.Select(segment => segment.Text)).Trim(), segments);
    }
}