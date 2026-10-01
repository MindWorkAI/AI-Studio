using System.Security.Cryptography;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    private const string OFFICE_LOCK_FILE_PREFIX = "~$";
    internal const int DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH = 300;
    private const bool IMAGE_EMBEDDING_ENABLED = false;

    /// <summary>
    /// What this build writes next to a chunk besides its text. Raise it whenever that changes.
    /// </summary>
    /// <remarks>
    /// A stored chunk keeps the metadata of the run which wrote it, and nothing recomputes it: the
    /// fingerprint of a file says whether the file changed, not whether we got better at reading
    /// it. Raising this number makes the embedding signature differ, which drops the index and
    /// builds it again — the only way corrected page numbers reach a data source somebody indexed
    /// earlier.
    ///
    /// Version 2: the page of a chunk is taken from the runtime metadata instead of being read back
    /// out of the chunk text, which is what left Word and OpenDocument files, and passages
    /// continuing across a page break, without a page.
    /// </remarks>
    private const string CHUNK_METADATA_VERSION = "2";

    private enum RagFileIndexingDecision
    {
        INDEXABLE,
        EXCLUDED,
        UNSUPPORTED,
    }

    private sealed record DataSourceMetadataSnapshot(string SourceHash, IReadOnlyDictionary<string, string> FileHashes);

    private async IAsyncEnumerable<EmbeddingChunk> StreamEmbeddingChunksAsync(string filePath, IDataSource dataSource, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var options = GetChunkingOptions(dataSource, embeddingProvider);
        var strategy = this.GetChunkingStrategy(filePath);
        var content = await this.ReadExtractedFileContentAsync(filePath, embeddingProvider, token);

        await foreach (var chunk in this.textChunker.SplitAsync(content, strategy, options, embeddingProvider, token))
            yield return chunk;
    }

    private async Task<SegmentedText> ReadExtractedFileContentAsync(string filePath, EmbeddingProvider embeddingProvider, CancellationToken token)
    {
        var segments = new List<TextSegment>();

        await foreach (var segment in rustService.StreamArbitraryFileDataWithTokenCounts(filePath, embeddingProvider, token))
        {
            var normalized = TextChunker.NormalizeSegment(segment.Content);
            if (!string.IsNullOrWhiteSpace(normalized))
                segments.Add(new(normalized, segment.TokenCount, segment.PageNumber));
        }

        return new(string.Join("\n", segments.Select(segment => segment.Text)).Trim(), segments);
    }

    /// <summary>
    /// Works out how the text of a data source is cut for a given embedding provider.
    /// </summary>
    /// <remarks>
    /// Static, because the answer follows from its two arguments alone. That lets the embedding
    /// signature be built for a configuration which is not stored yet, which is what the dialogs ask
    /// before they save a change.
    /// </remarks>
    /// <param name="dataSource">The data source whose own chunk settings apply.</param>
    /// <param name="embeddingProvider">The embedding provider whose token limit caps them.</param>
    /// <returns>The chunk size and overlap which are actually used.</returns>
    internal static ChunkingOptions GetChunkingOptions(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider)
    {
        var providerMaxChunkTokenLength = Math.Max(1, embeddingProvider.EffectiveTokenLimit);
        var dataSourceMaxChunkTokenLength = dataSource is IIndexedDataSource { MaxChunkTokenLength: > 0 } indexedDataSource
            ? indexedDataSource.MaxChunkTokenLength
            : 0;
        var maxChunkTokenLength = dataSourceMaxChunkTokenLength > 0
            ? Math.Min(dataSourceMaxChunkTokenLength, providerMaxChunkTokenLength)
            : providerMaxChunkTokenLength;

        var configuredOverlapTokenLength = dataSource is IIndexedDataSource overlapDataSource
            ? overlapDataSource.ChunkOverlapTokenLength
            : DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
        var overlapTokenLength = Math.Clamp(configuredOverlapTokenLength, 0, Math.Max(0, maxChunkTokenLength - 1));

        return new(maxChunkTokenLength, overlapTokenLength);
    }

    private ChunkingStrategy GetChunkingStrategy(string filePath)
    {
        if (this.IsPresentationFilePath(filePath))
            return TextChunker.PRESENTATION_STRATEGY;

        if (this.IsDelimitedTableFilePath(filePath) || this.IsSpreadsheetFilePath(filePath))
            return TextChunker.TABLE_STRATEGY;

        if (this.IsSourceCodeFilePath(filePath))
            return TextChunker.SOURCE_CODE_STRATEGY;

        return TextChunker.DOCUMENT_STRATEGY;
    }

    private FileEnumerationResult GetInputFiles(IDataSource dataSource)
    {
        var result = new FileEnumerationResult();

        switch (dataSource)
        {
            case DataSourceLocalFile localFile when File.Exists(localFile.FilePath):
                var file = new FileInfo(localFile.FilePath);
                switch (this.GetRagFileIndexingDecision(file))
                {
                    case RagFileIndexingDecision.INDEXABLE:
                        result.Files.Add(file);
                        break;

                    case RagFileIndexingDecision.EXCLUDED:
                        logger.LogDebug("Skipping excluded file '{FilePath}' while indexing.", file.FullName);
                        break;

                    default:
                        result.AddFailure(localFile.FilePath, string.Format(TB("The file '{0}' has a type AI Studio cannot index."), localFile.FilePath));
                        break;
                }

                return result;

            case DataSourceLocalDirectory localDirectory when Directory.Exists(localDirectory.Path):
                this.EnumerateAccessibleFiles(localDirectory.Path, result);
                return result;
        }

        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                result.AddFailure(localFile.FilePath, string.Format(TB("The file '{0}' does not exist."), localFile.FilePath));
                break;

            case DataSourceLocalDirectory localDirectory:
                result.AddFailure(localDirectory.Path, string.Format(TB("The folder '{0}' does not exist."), localDirectory.Path));
                break;
        }

        return result;
    }

    private void EnumerateAccessibleFiles(string rootPath, FileEnumerationResult result)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootPath);

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            IEnumerable<string> subDirectories;
            IEnumerable<string> files;

            try
            {
                subDirectories = Directory.EnumerateDirectories(currentPath);
                files = Directory.EnumerateFiles(currentPath);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Cannot access directory '{DirectoryPath}' while indexing.", currentPath);
                result.AddFailure(currentPath, string.Format(TB("The folder '{0}' could not be opened. Please check whether you are allowed to read it."), currentPath));
                continue;
            }

            foreach (var filePath in files)
            {
                FileInfo fileInfo;
                try
                {
                    fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                        continue;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", filePath);
                    result.AddFailure(filePath, string.Format(TB("The file '{0}' could not be read. Please check whether you are allowed to read it."), filePath));
                    continue;
                }

                switch (this.GetRagFileIndexingDecision(fileInfo))
                {
                    case RagFileIndexingDecision.INDEXABLE:
                        result.Files.Add(fileInfo);
                        break;

                    case RagFileIndexingDecision.EXCLUDED:
                        logger.LogDebug("Skipping excluded file '{FilePath}' while indexing.", fileInfo.FullName);
                        break;
                }
            }

            foreach (var subDirectory in subDirectories)
            {
                if (this.IsSkippedRagDirectory(subDirectory))
                    continue;

                pendingDirectories.Push(subDirectory);
            }
        }
    }

    private string TryGetRelativePath(IDataSource dataSource, FileInfo file) => dataSource switch
    {
        DataSourceLocalDirectory localDirectory => Path.GetRelativePath(localDirectory.Path, file.FullName),
        _ => file.Name
    };

    private bool IsImageFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.IMAGE);
    }

    private bool IsPresentationFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.POWER_POINT);
    }

    private bool IsDelimitedTableFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.TABULAR);
    }

    private bool IsSpreadsheetFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.SPREADSHEET);
    }

    private bool IsSourceCodeFilePath(string filePath)
    {
        return !this.IsHtmlFilePath(filePath) && FileTypes.IsAllowedPath(filePath, FileTypes.SOURCE_CODE);
    }

    private bool IsHtmlFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.HTML);
    }

    private bool IsSupportedRagFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT);
    }

    private RagFileIndexingDecision GetRagFileIndexingDecision(FileInfo file)
    {
        if (this.IsSkippedRagFile(file))
            return RagFileIndexingDecision.EXCLUDED;

        if (!IMAGE_EMBEDDING_ENABLED && this.IsImageFilePath(file.FullName))
            return RagFileIndexingDecision.EXCLUDED;

        return this.IsSupportedRagFilePath(file.FullName)
            ? RagFileIndexingDecision.INDEXABLE
            : RagFileIndexingDecision.UNSUPPORTED;
    }

    private bool IsSkippedRagFile(FileInfo file)
    {
        if (IsSkippedRagFileName(file.Name))
            return true;

        try
        {
            return file.Attributes.HasFlag(FileAttributes.ReparsePoint)
                   || file.Attributes.HasFlag(FileAttributes.Offline)
                   || file.Attributes.HasFlag(FileAttributes.Temporary)
                   || file.Attributes.HasFlag(FileAttributes.System);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot inspect file '{FilePath}' while indexing.", file.FullName);
            return true;
        }
    }

    private static bool IsSkippedRagFileName(string fileName)
    {
        return FileTypes.IsAllowedPath(fileName, FileTypes.SHORTCUT)
               || fileName.StartsWith(OFFICE_LOCK_FILE_PREFIX, StringComparison.Ordinal);
    }

    private bool IsSkippedRagDirectory(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            return directory.Attributes.HasFlag(FileAttributes.ReparsePoint)
                   || directory.Attributes.HasFlag(FileAttributes.Offline)
                   || directory.Attributes.HasFlag(FileAttributes.System);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot inspect directory '{DirectoryPath}' while indexing.", path);
            return true;
        }
    }

    /// <summary>
    /// Describes how the vectors of a data source were made.
    /// </summary>
    /// <remarks>
    /// What appears here decides when stored embeddings are thrown away: a signature differing from
    /// the persisted one drops the whole index and builds it again. So it names the embedding model,
    /// where it runs, how the text was cut for it, and the chunk metadata version — the things a
    /// vector actually depends on.
    ///
    /// Two of them are less obvious than they look. The Hugging Face inference provider belongs to
    /// where the model runs: the same model name served by another backend is another vector source.
    /// And a custom tokenizer enters through its content, not through its path, because a tokenizer
    /// is stored under the name it came with — almost always tokenizer.json — so swapping one for
    /// another lands on the identical path, while moving the data directory changes every path
    /// without changing a single tokenizer.
    ///
    /// The chunk settings enter only as what they amount to, never as what somebody typed. A data
    /// source storing 0 means "follow the embedding provider", and writing that provider's own limit
    /// into the field changes nothing about how the text is cut. Carrying the typed numbers as well
    /// made that a different signature, so opening the expert settings of a data source — which
    /// fills an empty limit with the provider's — threw the whole index away for nothing.
    ///
    /// The confidence level a data source asks of a provider is deliberately not among them. It
    /// changes no vector, and it is enforced live on every request anyway: DataSourceService checks
    /// it against the participating chat providers and against the embedding provider, and this
    /// service checks it again before each indexing run. It was part of this signature once, which
    /// re-embedded every file of a data source whenever somebody raised or lowered it — real money
    /// at a cloud embedding provider, for nothing.
    /// </remarks>
    internal static string BuildEmbeddingSignature(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider, ChunkingOptions chunkingOptions)
    {
        return string.Join('|',
            CHUNK_METADATA_VERSION,
            embeddingProvider.Id,
            embeddingProvider.UsedLLMProvider,
            embeddingProvider.Model.Id,
            embeddingProvider.Host,
            embeddingProvider.Hostname,
            embeddingProvider.HFInferenceProvider,
            embeddingProvider.TokenizerFingerprint,
            embeddingProvider.EffectiveTokenLimit,
            chunkingOptions.MaxChunkTokenLength,
            chunkingOptions.OverlapTokenLength);
    }

    /// <summary>
    /// Describes how the vectors of a data source were made, working the chunking out along the way.
    /// </summary>
    /// <param name="dataSource">The data source the vectors belong to.</param>
    /// <param name="embeddingProvider">The embedding provider which makes them.</param>
    /// <returns>The signature of this pairing.</returns>
    internal static string BuildEmbeddingSignature(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider) =>
        BuildEmbeddingSignature(dataSource, embeddingProvider, GetChunkingOptions(dataSource, embeddingProvider));

    private DataSourceMetadataSnapshot BuildDataSourceMetadataSnapshot(IDataSource dataSource, IReadOnlyList<FileInfo> indexedFiles)
    {
        var fileHashes = indexedFiles
            .OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(file => file.FullName, BuildFileMetadataHash, StringComparer.OrdinalIgnoreCase);

        var sourceHash = dataSource switch
        {
            DataSourceLocalFile localFile => indexedFiles.Count > 0
                ? fileHashes[indexedFiles[0].FullName]
                : BuildMetadataHash("file", localFile.FilePath, Path.GetFileName(localFile.FilePath), "missing", "0"),

            DataSourceLocalDirectory localDirectory => this.BuildDirectoryMetadataHash(localDirectory, indexedFiles, fileHashes),

            _ => BuildMetadataHash(dataSource.Type.ToString(), dataSource.Id, dataSource.Name)
        };

        return new(sourceHash, fileHashes);
    }

    private string BuildDirectoryMetadataHash(DataSourceLocalDirectory dataSource, IReadOnlyList<FileInfo> indexedFiles, IReadOnlyDictionary<string, string> fileHashes)
    {
        var directory = new DirectoryInfo(dataSource.Path);
        directory.Refresh();

        var totalSize = 0L;
        var latestFileWriteTicks = 0L;
        foreach (var file in indexedFiles)
        {
            file.Refresh();
            if (!file.Exists)
                continue;

            totalSize += file.Length;
            latestFileWriteTicks = Math.Max(latestFileWriteTicks, file.LastWriteTimeUtc.Ticks);
        }

        var latestWriteTicks = Math.Max(directory.LastWriteTimeUtc.Ticks, latestFileWriteTicks);
        var parts = new List<string>
        {
            "directory",
            directory.FullName,
            directory.Name,
            latestWriteTicks.ToString(),
            totalSize.ToString(),
            indexedFiles.Count.ToString()
        };

        foreach (var file in indexedFiles.OrderBy(file => file.FullName, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(this.TryGetRelativePath(dataSource, file));
            parts.Add(fileHashes[file.FullName]);
        }

        return BuildMetadataHash(parts);
    }

    private static string BuildFileMetadataHash(FileInfo file)
    {
        file.Refresh();
        if (!file.Exists)
        {
            return BuildMetadataHash(
                "file",
                file.FullName,
                file.Name,
                "missing",
                "0");
        }

        return BuildMetadataHash(
            "file",
            file.FullName,
            file.Name,
            file.LastWriteTimeUtc.Ticks.ToString(),
            file.Length.ToString());
    }

    private static string BuildMetadataHash(params string[] parts)
    {
        return BuildMetadataHash((IEnumerable<string>)parts);
    }

    private static string BuildMetadataHash(IEnumerable<string> parts)
    {
        var fingerprintSource = new StringBuilder();
        foreach (var part in parts)
            fingerprintSource.Append(part.Length).Append(':').Append(part).Append('|');

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource.ToString()));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Describes one file as a document for the shared part of an indexing run.
    /// </summary>
    /// <param name="context">The run the file is indexed in.</param>
    /// <param name="dataSource">The data source the file belongs to.</param>
    /// <param name="file">The file.</param>
    /// <param name="fingerprint">The fingerprint of the file as it is about to be read.</param>
    /// <returns>The document.</returns>
    private EmbeddingDocument CreateFileDocument(IndexedRunContext context, IDataSource dataSource, FileInfo file, string fingerprint) => new(
        file.FullName,
        this.CreateEmbeddingStateFile(dataSource, file, fingerprint, 0, DateTimeOffset.UtcNow),
        file.Name,
        token => this.StreamEmbeddingChunksAsync(file.FullName, dataSource, context.EmbeddingProvider, token));

    private EmbeddingStateFile CreateEmbeddingStateFile(IDataSource dataSource, FileInfo file, string fingerprint, int chunkCount, DateTimeOffset embeddedAtUtc)
    {
        file.Refresh();
        var absolutePath = Path.GetFullPath(file.FullName);
        return new(
            IndexedDocumentIds.CreateParentId(dataSource.Id, absolutePath),
            absolutePath,
            file.Name,
            this.TryGetRelativePath(dataSource, file),
            GetFileType(file),
            fingerprint,
            file.Exists ? file.Length : 0,
            file.Exists ? new DateTimeOffset(file.CreationTimeUtc) : DateTimeOffset.UnixEpoch,
            file.Exists ? new DateTimeOffset(file.LastWriteTimeUtc) : DateTimeOffset.UnixEpoch,
            embeddedAtUtc,
            chunkCount);
    }

    private static string GetFileType(FileInfo file)
    {
        var extension = file.Extension.TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(extension) ? "unknown" : extension;
    }
}
