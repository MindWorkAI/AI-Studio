using System.Security.Cryptography;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

using static AIStudio.Tools.Services.Indexing.IndexingLogFormat;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Indexes local files and folders.
/// </summary>
/// <remarks>
/// A file is a document under its full path. Whether it changed is told by a fingerprint over its
/// path, name, size and write time, so an unchanged folder is worked through without reading a
/// single file. Reading one goes through the runtime, which extracts the text and filters it.
///
/// Changes are noticed by a file system watcher per data source. A burst of changes is waited out
/// before a run is asked for, so saving a document twice in a row costs one run, not two.
/// </remarks>
/// <param name="settingsManager">The settings, read for whether local data sources refresh on their own.</param>
/// <param name="rustService">The runtime, which extracts the text of the files.</param>
/// <param name="guardService">The prompt injection filter, whose findings are reported once per run.</param>
/// <param name="textChunker">Cuts the text of a file into chunks.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed partial class FileSourceIndexer(SettingsManager settingsManager, RustService rustService, PromptInjectionGuardService guardService, TextChunker textChunker, ILogger logger) : IIndexedSourceIndexer
{
    private const string OFFICE_LOCK_FILE_PREFIX = "~$";
    private const bool IMAGE_EMBEDDING_ENABLED = false;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileSourceIndexer).Namespace, nameof(FileSourceIndexer));

    private enum RagFileIndexingDecision
    {
        INDEXABLE,
        EXCLUDED,
        UNSUPPORTED,
    }

    private sealed record DataSourceMetadataSnapshot(string SourceHash, IReadOnlyDictionary<string, string> FileHashes);

    /// <inheritdoc />
    public bool Supports(IDataSourceBase dataSource) => dataSource is DataSourceLocalDirectory or DataSourceLocalFile;

    /// <inheritdoc />
    public async Task<IndexedRunOutcome> ProcessAsync(IndexedRunContext context, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (context.DataSource is not IDataSource dataSource || !this.Supports(dataSource))
            throw new ArgumentException("The file indexer reads local files and folders only.", nameof(context));

        var manifest = context.Manifest;

        var inputFiles = this.GetInputFiles(dataSource);
        var indexedFiles = inputFiles.Files;
        var totalFiles = indexedFiles.Count + inputFiles.FailedFiles;

        foreach (var failure in inputFiles.Failures)
        {
            logger.LogWarning(
                "Cannot index data source input '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}). Reason='{Reason}'.",
                failure.DocumentKey,
                dataSource.Name,
                dataSource.Id,
                failure.Reason);
        }

        logger.LogInformation(
            "Prepared data source '{DataSourceName}' ({DataSourceId}) for embedding. AccessibleFiles={AccessibleFiles}, FailedFiles={FailedFiles}, Collection='{CollectionName}'.",
            dataSource.Name,
            dataSource.Id,
            indexedFiles.Count,
            inputFiles.FailedFiles,
            context.CollectionName);

        var metadataSnapshot = this.BuildDataSourceMetadataSnapshot(dataSource, indexedFiles);
        var removedMissingFiles = await this.RemoveMissingFileEmbeddingsAsync(context, indexedFiles, token);
        token.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Compared data source hash for '{DataSourceName}' ({DataSourceId}). StoredSourceHashPrefix={StoredSourceHashPrefix}, CurrentSourceHashPrefix={CurrentSourceHashPrefix}, StoredFileRecords={StoredFileRecords}, CurrentFiles={CurrentFiles}, RemovedMissingFiles={RemovedMissingFiles}.",
            dataSource.Name,
            dataSource.Id,
            ShortHash(manifest.SourceHash),
            ShortHash(metadataSnapshot.SourceHash),
            manifest.Files.Count,
            indexedFiles.Count,
            removedMissingFiles);

        var progress = new DocumentRunProgress(context, totalFiles, inputFiles.FailedFiles, inputFiles.LastError, inputFiles.Failures, logger);
        if (CanSkipDataSourceByHash(manifest, metadataSnapshot, indexedFiles))
        {
            logger.LogInformation(
                "Skipping data source '{DataSourceName}' ({DataSourceId}) because the persisted data source hash and all persisted file hashes match. RefreshMode={RefreshMode}, PermanentlySkippedFiles={PermanentlySkippedFiles}.",
                dataSource.Name,
                dataSource.Id,
                refreshMode,
                manifest.PermanentFailures.Count);

            //
            // The files which were skipped for good are none of the indexed ones, and their stored
            // reasons belong into the list even on a run which read nothing at all:
            //
            progress.RecordUnchanged(indexedFiles.Count - manifest.PermanentFailures.Count);
            foreach (var (filePath, permanentFailure) in manifest.PermanentFailures)
                progress.RecordStillUnreadable(filePath, permanentFailure);

            await progress.CompleteRunAsync(metadataSnapshot.SourceHash, "data source finished after removing missing files", token);
            return IndexedRunOutcome.DONE;
        }

        token.ThrowIfCancellationRequested();
        progress.Publish();

        //
        // Everything the runtime filters out of these files is reported once for the whole data
        // source. A run over a few thousand documents which removes something in forty of them
        // is one thing that happened to the user, not forty. The scope ends with this method, so
        // the report arrives when the run is finished rather than in the middle of it.
        //
        await using var promptInjectionReportingScope = guardService.BeginAction();

        foreach (var file in indexedFiles)
        {
            token.ThrowIfCancellationRequested();

            var fingerprint = metadataSnapshot.FileHashes[file.FullName];
            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord) &&
                string.Equals(existingRecord.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping unchanged file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because the persisted metadata hash matches. MetadataHashPrefix={MetadataHashPrefix}, LastWriteUtc={LastWriteUtc:O}, FileSize={FileSize}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    ShortHash(fingerprint),
                    file.LastWriteTimeUtc,
                    file.Length);
                progress.RecordUnchanged();
                progress.Publish();
                continue;
            }

            //
            // A file which failed for a reason of its own is not read again until it changes.
            // Without this, a folder holding hundreds of scanned documents without a text layer
            // would spend half an hour on every start to arrive at the result we already have:
            //
            if (manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure) &&
                string.Equals(permanentFailure.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                logger.LogDebug(
                    "Skipping file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because reading it failed permanently before. FailureCode={FailureCode}, MetadataHashPrefix={MetadataHashPrefix}, OccurredAtUtc={OccurredAtUtc:O}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    permanentFailure.Code,
                    ShortHash(fingerprint),
                    permanentFailure.OccurredAtUtc);
                progress.RecordStillUnreadable(file.FullName, permanentFailure);
                progress.Publish();
                continue;
            }

            var document = this.CreateFileDocument(context, dataSource, file, fingerprint);
            var reportBlockProgress = progress.BeginDocument(document);

            try
            {
                logger.LogInformation(
                    "Embedding file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because {EmbeddingReason}. CurrentMetadataHashPrefix={CurrentMetadataHashPrefix}. Progress={CompletedFiles}/{TotalFiles}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    GetFileEmbeddingReason(file, fingerprint, existingRecord),
                    ShortHash(fingerprint),
                    progress.DoneDocuments + 1,
                    totalFiles);
                var startedAtUtc = DateTimeOffset.UtcNow;
                var chunkCount = await context.IndexDocumentAsync(document, reportBlockProgress, token);
                token.ThrowIfCancellationRequested();
                var fingerprintAfterEmbedding = BuildFileMetadataHash(file);
                if (!string.Equals(fingerprint, fingerprintAfterEmbedding, StringComparison.Ordinal))
                    throw new IOException(string.Format(TB("The file '{0}' changed while it was being indexed. What was indexed of it is discarded, and the file is tried again during the next run."), file.FullName));

                await progress.RecordDocumentIndexedAsync(document, chunkCount, existingRecord is null, token);
                logger.LogInformation(
                    "Embedded file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) successfully. Chunks={ChunkCount}, DurationMs={DurationMs}.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    chunkCount,
                    (DateTimeOffset.UtcNow - startedAtUtc).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (VectorStoreUnreadableException)
            {
                //
                // Not about this one file: the store of the whole data source cannot be opened, so
                // every remaining file would fail the same way. Carrying on would fill the list
                // with one entry per file and hide the single cause behind them.
                //
                throw;
            }
            catch (Exception exception)
            {
                await progress.RecordDocumentFailureAsync(document, exception, token);
            }
        }

        await progress.CompleteRunAsync(metadataSnapshot.SourceHash, "data source embedding run finished", token);
        logger.LogInformation(
            "Finished background embeddings for data source '{DataSourceName}' ({DataSourceId}). RefreshMode={RefreshMode}, Embedded={EmbeddedFiles}, New={NewFiles}, Changed={ChangedFiles}, Skipped={SkippedFiles}, PermanentlySkipped={PermanentlySkippedFiles}, RemovedMissing={RemovedMissingFiles}, Failed={FailedFiles}, Total={TotalFiles}, SourceHashPrefix={SourceHashPrefix}.",
            dataSource.Name,
            dataSource.Id,
            refreshMode,
            progress.IndexedDocuments,
            progress.NewDocuments,
            progress.ChangedDocuments,
            progress.UnchangedDocuments,
            progress.PermanentlySkippedDocuments,
            removedMissingFiles,
            progress.FailedDocuments,
            totalFiles,
            ShortHash(metadataSnapshot.SourceHash));

        return IndexedRunOutcome.DONE;
    }

    /// <summary>
    /// Whether a file name marks a file which is never indexed, whatever its type.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>True for shortcuts and the lock files of office suites.</returns>
    private static bool IsSkippedRagFileName(string fileName)
    {
        return FileTypes.IsAllowedPath(fileName, FileTypes.SHORTCUT)
               || fileName.StartsWith(OFFICE_LOCK_FILE_PREFIX, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a file has a type whose text can be indexed.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>True for every document type the runtime can read.</returns>
    private static bool IsSupportedRagFilePath(string filePath)
    {
        return FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT);
    }

    private async Task<int> RemoveMissingFileEmbeddingsAsync(IndexedRunContext context, IReadOnlyCollection<FileInfo> indexedFiles, CancellationToken token)
    {
        var manifest = context.Manifest;
        var existingPaths = indexedFiles
            .Select(file => file.FullName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removedFiles = 0;
        foreach (var removedFilePath in manifest.Files.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            await context.DeleteDocumentPointsAsync(removedFilePath, token);
            await context.IndexStore.DeleteFileAsync(context.DataSource.Id, removedFilePath, token);
            manifest.Files.Remove(removedFilePath);
            removedFiles++;
            logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                context.DataSource.Name,
                context.DataSource.Id);
        }

        //
        // A file which is gone needs no mark keeping it out of the index. Without this, the table
        // would grow with every document the user ever deleted:
        //
        foreach (var removedFilePath in manifest.PermanentFailures.Keys.Except(existingPaths, StringComparer.OrdinalIgnoreCase).ToList())
            await context.ForgetPermanentFailureAsync(removedFilePath, token);

        return removedFiles;
    }

    /// <remarks>
    /// A file counts as settled when it was indexed or when it was skipped for good, both with a
    /// matching fingerprint. Counting only the indexed ones would let a single unreadable document
    /// send the whole folder through the slow path on every run.
    /// </remarks>
    private static bool CanSkipDataSourceByHash(DataSourceEmbeddingManifest manifest, DataSourceMetadataSnapshot metadataSnapshot, IReadOnlyCollection<FileInfo> indexedFiles)
    {
        if (!string.Equals(manifest.SourceHash, metadataSnapshot.SourceHash, StringComparison.Ordinal))
            return false;

        if (manifest.Files.Count + manifest.PermanentFailures.Count != indexedFiles.Count)
            return false;

        foreach (var file in indexedFiles)
        {
            if (!metadataSnapshot.FileHashes.TryGetValue(file.FullName, out var currentHash))
                return false;

            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord))
            {
                if (!string.Equals(existingRecord.Fingerprint, currentHash, StringComparison.Ordinal))
                    return false;

                continue;
            }

            if (!manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure))
                return false;

            if (!string.Equals(permanentFailure.Fingerprint, currentHash, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string GetFileEmbeddingReason(FileInfo file, string currentHash, EmbeddedFileRecord? existingRecord)
    {
        if (existingRecord is null)
            return "no stored file hash exists";

        var reasons = new List<string>();
        if (!string.Equals(existingRecord.Fingerprint, currentHash, StringComparison.Ordinal))
            reasons.Add($"stored hash {ShortHash(existingRecord.Fingerprint)} differs from current hash {ShortHash(currentHash)}");

        if (existingRecord.FileSize != file.Length)
            reasons.Add($"file size changed from {existingRecord.FileSize} to {file.Length} bytes");

        if (existingRecord.LastWriteUtc != new DateTimeOffset(file.LastWriteTimeUtc))
            reasons.Add($"last modified time changed from {existingRecord.LastWriteUtc:O} to {file.LastWriteTimeUtc:O}");

        return reasons.Count == 0
            ? "the file hash changed"
            : string.Join("; ", reasons);
    }

    private async IAsyncEnumerable<EmbeddingChunk> StreamEmbeddingChunksAsync(string filePath, IDataSource dataSource, EmbeddingProvider embeddingProvider, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var options = DataSourceEmbeddingService.GetChunkingOptions(dataSource, embeddingProvider);
        var strategy = this.GetChunkingStrategy(filePath);
        var content = await this.ReadExtractedFileContentAsync(filePath, embeddingProvider, token);

        await foreach (var chunk in textChunker.SplitAsync(content, strategy, options, embeddingProvider, token))
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

    private RagFileIndexingDecision GetRagFileIndexingDecision(FileInfo file)
    {
        if (this.IsSkippedRagFile(file))
            return RagFileIndexingDecision.EXCLUDED;

        if (!IMAGE_EMBEDDING_ENABLED && this.IsImageFilePath(file.FullName))
            return RagFileIndexingDecision.EXCLUDED;

        return IsSupportedRagFilePath(file.FullName)
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