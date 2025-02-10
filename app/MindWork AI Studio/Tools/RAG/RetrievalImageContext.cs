using AIStudio.Chat;

namespace AIStudio.Tools.RAG;

public sealed class RetrievalImageContext : IRetrievalContext
{
    #region Implementation of IRetrievalContext

    public required string DataSourceName { get; init; }

    public required RetrievalContentCategory Category { get; init; }

    public required RetrievalContentType Type { get; init; }

    public required string Path { get; init; }

    public IReadOnlyList<string> Links { get; init; } = [];

    #endregion

    /// <summary>
    /// The type of the image source.
    /// </summary>
    /// <remarks>
    /// Is the image source a URL, a local file path, a base64 string, etc.?
    /// </remarks>
    public required ContentImageSource SourceType { get; init; }

    /// <summary>
    /// The image source.
    /// </summary>
    public required string Source { get; set; }
}