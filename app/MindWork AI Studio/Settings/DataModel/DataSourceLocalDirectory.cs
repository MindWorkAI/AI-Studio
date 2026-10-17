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
    public const bool DEFAULT_INCLUDE_HIDDEN_FILES_AND_FOLDERS = false;

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
    /// The files and folders below the path which are left out, even when a file pattern selects
    /// them, cf. FileExclusions. Data sources from before this list existed get the default list,
    /// so their build outputs and dependencies leave the index during the next run.
    /// </summary>
    public string ExcludePatterns { get; init; } = FileExclusions.DEFAULT_PATTERNS;

    /// <summary>
    /// The largest file below the path which is indexed, in megabytes. A larger file which the
    /// patterns select is not indexed, and the embedding page tells the user about it.
    /// </summary>
    public int MaxFileSizeMegabytes { get; init; } = FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES;

    /// <summary>
    /// Whether files in folders below <see cref="this.Path"/> are considered for indexing.
    /// </summary>
    public bool IncludeSubdirectories { get; init; } = DEFAULT_INCLUDE_SUBDIRECTORIES;

    /// <summary>
    /// Whether hidden files and the files in hidden folders below the path are considered for
    /// indexing. Hidden are those whose names begin with a dot and those the operating system hides.
    /// </summary>
    /// <remarks>
    /// Even while this is off, a file pattern which begins with a dot, such as .gitignore, selects
    /// the hidden files it names, cf. FileNamePatterns.NamesHiddenFile. Hidden folders are entered
    /// only while this is on.
    /// </remarks>
    public bool IncludeHiddenFilesAndFolders { get; init; } = DEFAULT_INCLUDE_HIDDEN_FILES_AND_FOLDERS;

    /// <summary>
    /// The patterns used at runtime. An invalid value can only come from malformed or hand-edited
    /// settings; falling back to the default, which selects what folder data sources indexed before
    /// they had file patterns, prevents an accidental removal of the index.
    /// </summary>
    [JsonIgnore]
    public string EffectiveFilePatterns => FileNamePatterns.IsValid(this.FilePatterns)
        ? this.FilePatterns
        : DEFAULT_FILE_PATTERNS;

    /// <summary>
    /// The exclusion list used at runtime. Like the patterns, an invalid value can only come from
    /// malformed or hand-edited settings, and the default list takes its place.
    /// </summary>
    [JsonIgnore]
    public string EffectiveExcludePatterns => FileExclusions.IsValid(this.ExcludePatterns)
        ? this.ExcludePatterns
        : FileExclusions.DEFAULT_PATTERNS;

    /// <summary>
    /// The size limit used at runtime. A limit below the smallest one can only come from malformed
    /// or hand-edited settings, and the default takes its place.
    /// </summary>
    [JsonIgnore]
    public int EffectiveMaxFileSizeMegabytes => this.MaxFileSizeMegabytes >= FolderFileSelection.SMALLEST_MAX_FILE_SIZE_MEGABYTES
        ? this.MaxFileSizeMegabytes
        : FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES;
}
