namespace AIStudio.Tools.RAG;

/// <summary>
/// One passage a data source handed to the AI for an answer, as the AI received it.
/// </summary>
/// <remarks>
/// Stored with the answer, so the user can still read what the answer rests on after the chat was
/// reloaded. The texts are the filtered ones: whatever the prompt injection filter removed did not
/// reach the AI, so it is not shown as if it had.<br/><br/>
/// A passage belongs to the source it is listed under, which is why it is only made for a context
/// with a source, see SourceUrl. A context without one, e.g., a file known by a relative path only,
/// has nothing below the answer to show its passage at, so it gets none.<br/><br/>
/// Plain stored values only: a computed property would be written into every chat file as well.
/// </remarks>
public sealed record SourcePassage
{
    /// <summary>
    /// The address of the source this passage is listed under.
    /// </summary>
    /// <remarks>
    /// The first source of the context, i.e., its own reference, such as the file with the page of
    /// the passage. The list of sources tells sources apart by their address without the fragment,
    /// and so does the search for the passages of a source, see GetPassagesOf.
    /// </remarks>
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// The name of where the passage was found, e.g., a document or a database.
    /// </summary>
    public string DataSourceName { get; init; } = string.Empty;

    /// <summary>
    /// The path to the content, e.g., a file path or a URL.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The category of the content, like text or image.
    /// </summary>
    public RetrievalContentCategory Category { get; init; }

    /// <summary>
    /// What type of content the passage is, like a document or a transcript.
    /// </summary>
    public RetrievalContentType Type { get; init; }

    /// <summary>
    /// The matched text, filtered for prompt injections.
    /// </summary>
    /// <remarks>
    /// Empty for an image which matched: the AI is only told that it did, never shown the image.
    /// </remarks>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The content around the matched text, filtered for prompt injections.
    /// </summary>
    public IReadOnlyList<string> SurroundingContent { get; init; } = [];

    /// <summary>
    /// The page the passage starts on, or null when it has none.
    /// </summary>
    public int? PageNumber { get; init; }

    /// <summary>
    /// The page the passage ends on, or null when it is not known.
    /// </summary>
    public int? EndPageNumber { get; init; }
}