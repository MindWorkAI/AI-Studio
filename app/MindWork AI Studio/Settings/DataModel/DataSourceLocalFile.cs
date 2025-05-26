using AIStudio.Chat;
using AIStudio.Tools.RAG;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Represents one local file as a data source.
/// </summary>
public readonly record struct DataSourceLocalFile : IInternalDataSource
{
    public DataSourceLocalFile()
    {
    }

    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;
    
    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string EmbeddingId { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public DataSourceSecurity SecurityPolicy { get; init; } = DataSourceSecurity.NOT_SPECIFIED;
    
    /// <inheritdoc />
    public ushort MaxMatches { get; init; } = 10;
    
    /// <inheritdoc />
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastPrompt, ChatThread thread, CancellationToken token = default)
    {
        IReadOnlyList<IRetrievalContext> retrievalContext = new List<IRetrievalContext>();
        return Task.FromResult(retrievalContext);
    }
    
    /// <summary>
    /// The path to the file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
}