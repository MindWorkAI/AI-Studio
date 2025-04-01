using AIStudio.Chat;

namespace AIStudio.Tools.RAG;

public sealed class RetrievalImageContext : IRetrievalContext, IImageSource
{
    #region Implementation of IRetrievalContext

    /// <inheritdoc />
    public required string DataSourceName { get; init; }

    /// <inheritdoc />
    public required RetrievalContentCategory Category { get; init; }

    /// <inheritdoc />
    public required RetrievalContentType Type { get; init; }

    /// <inheritdoc />
    public required string Path { get; init; }

    /// <inheritdoc />
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