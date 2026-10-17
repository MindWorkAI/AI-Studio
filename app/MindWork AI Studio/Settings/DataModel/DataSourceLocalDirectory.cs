using System.Text.Json.Serialization;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Represents a local directory as a data source.
/// </summary>
public readonly record struct DataSourceLocalDirectory : IInternalDataSource
{
    // Selects what folder data sources indexed before they had file patterns, plus every other
    // file whose content is readable text, e.g., a Terraform file or a script without an extension.
    // Existing data sources get it as well, so an update neither adds images nor binaries to them:
    public const string DEFAULT_FILE_PATTERNS = FileNamePatterns.ALL_DOCUMENTS_PATTERN;

    // Folder data sources were recursive before this option existed. Keeping true as the
    // deserialization default preserves every document of existing data sources.
    public const bool DEFAULT_INCLUDE_SUBDIRECTORIES = true;
    public const bool DEFAULT_INCLUDE_HIDDEN_SUBDIRECTORIES = false;

    public DataSourceLocalDirectory()
    {
    }

    /// <inheritdoc />
    public uint Num { get; init; }

    /// <inheritdoc />
    public string Id { get; init; } = Guid.Empty.ToString();
    
    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The description of the data source. What kind of data does it contain?
    /// What is the data source used for?
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc />
    public DataSourceType Type { get; init; } = DataSourceType.NONE;
    
    /// <inheritdoc />
    public string EmbeddingId { get; init; } = Guid.Empty.ToString();

    /// <inheritdoc />
    public int MaxChunkTokenLength { get; init; }

    /// <inheritdoc />
    public int ChunkOverlapTokenLength { get; init; } = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
    
    /// <inheritdoc />
    public ConfidenceLevel ConfidenceLevel { get; init; } = ConfidenceLevel.UNKNOWN;

    /// <inheritdoc />
    public bool IsEnterpriseConfiguration { get; init; }

    /// <inheritdoc />
    public Guid EnterpriseConfigurationPluginId { get; init; } = Guid.Empty;
    
    /// <inheritdoc />
    public ushort MaxMatches { get; init; } = 10;
    
    /// <inheritdoc />
    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        Program.SERVICE_PROVIDER.GetRequiredService<DataSourceLocalRetrievalService>().RetrieveDataAsync(this, lastUserPrompt, thread, token);
    
    /// <inheritdoc />
    public Task<RetrievalPage> RetrieveDataAsync(string query, int page, ChatThread thread, CancellationToken token = default) =>
        Program.SERVICE_PROVIDER.GetRequiredService<DataSourceLocalRetrievalService>().RetrieveDataAsync(this, query, page, thread, token);
    
    /// <summary>
    /// The path to the directory.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Semicolon-separated file-name patterns which decide which files below <see cref="this.Path"/>
    /// are considered for indexing.
    /// </summary>
    public string FilePatterns { get; init; } = DEFAULT_FILE_PATTERNS;

    /// <summary>
    /// Whether files in folders below <see cref="this.Path"/> are considered for indexing.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = DEFAULT_INCLUDE_SUBDIRECTORIES;

    /// <summary>
    /// Whether files in hidden folders below <see cref="this.Path"/> are considered for indexing.
    /// Has no effect when <see cref="this.IncludeSubdirectories"/> is false.
    /// </summary>
    public bool IncludeHiddenSubdirectories { get; init; } = DEFAULT_INCLUDE_HIDDEN_SUBDIRECTORIES;

    /// <summary>
    /// The patterns used at runtime. An invalid value can only come from malformed or hand-edited
    /// settings; falling back to the default, which selects what folder data sources indexed before
    /// they had file patterns, prevents an accidental removal of the index.
    /// </summary>
    [JsonIgnore]
    public string EffectiveFilePatterns => FileNamePatterns.IsValid(this.FilePatterns)
        ? this.FilePatterns
        : DEFAULT_FILE_PATTERNS;
}
