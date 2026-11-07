namespace AIStudio.Chat.Compression;

/// <summary>
/// The summary the AI wrote of a chat, or why there is none.
/// </summary>
/// <param name="Success">Whether there is a summary.</param>
/// <param name="Summary">The summary, as the AI wrote it. Not filtered for prompt injections yet.</param>
/// <param name="Issue">Why there is no summary, for the user. Empty on success.</param>
public sealed record ChatCompressionResult(bool Success, string Summary, string Issue)
{
    public static ChatCompressionResult Succeeded(string summary) => new(true, summary, string.Empty);

    public static ChatCompressionResult Failed(string issue) => new(false, string.Empty, issue);
}