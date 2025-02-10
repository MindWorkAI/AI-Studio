namespace AIStudio.Tools.RAG;

/// <summary>
/// The retrieval context for text data.
/// </summary>
public sealed class RetrievalTextContext : IRetrievalContext
{
    #region Implementation of IRetrievalContext
    
    public required string DataSourceName { get; init; }
    
    public required RetrievalContentCategory Category { get; init; }
    
    public required RetrievalContentType Type { get; init; }
    
    public required string Path { get; init; }

    public IReadOnlyList<string> Links { get; init; } = [];

    #endregion

    /// <summary>
    /// The text content.
    /// </summary>
    /// <remarks>
    /// Should contain the matched text and some small context around it.
    /// </remarks>
    public required string MatchedText { get; set; }

    /// <summary>
    /// The surrounding content of the matched text.
    /// </summary>
    /// <remarks>
    /// Might give the user some context about the matched text.
    /// For example, one sentence or paragraph before and after the matched text.
    /// </remarks>
    public IReadOnlyList<string> SurroundingContent { get; set; } = [];
}