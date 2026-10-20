using System.Security.Cryptography;
using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Media;
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
///
/// A run reads files for its share of the time, cf. Share, and leaves the rest to the next one, so
/// a folder which takes hours does not hold up every other data source. Every such step lists the
/// folder anew, and counts what needs no reading before it reads the first file. A step stores no
/// hash, so a folder which is indexed for the first time stays out of every search until its index
/// is complete, cf. DocumentRunProgress.PauseRunAsync.
/// </remarks>
/// <param name="settingsManager">The settings, read for whether local data sources refresh on their own.</param>
/// <param name="rustService">The runtime, which extracts the text of the files.</param>
/// <param name="mediaTranscriptionService">Transcribes selected audio and video files.</param>
/// <param name="textChunker">Cuts the text of a file into chunks.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed partial class FileSourceIndexer(SettingsManager settingsManager, RustService rustService, MediaTranscriptionService mediaTranscriptionService, TextChunker textChunker, ILogger logger) : IIndexedSourceIndexer
{
    private const bool IMAGE_EMBEDDING_ENABLED = false;
    private const int MAX_LOGGED_EXCLUDED_FILE_EXAMPLES = 3;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileSourceIndexer).Namespace, nameof(FileSourceIndexer));

    private enum RagFileIndexingDecision
    {
        INDEXABLE,
        EXCLUDED,
        IMAGE_NOT_SUPPORTED,
        TRANSCRIPTION_PROVIDER_MISSING,
        TRANSCRIPTION_PROVIDER_NOT_ALLOWED,
        TOO_LARGE,
    }

    private sealed record DataSourceMetadataSnapshot(string SourceHash, IReadOnlyDictionary<string, string> FileHashes);

    /// <summary>
    /// What decides about the files of a data source while they are listed.
    /// </summary>
    /// <param name="Selection">The files of a folder data source, or null for a single file, which has no size limit.</param>
    /// <param name="Manifest">What the index stores about the data source, or null when it does not matter.</param>
    /// <param name="MediaTranscription">Which transcription provider may hear audio and video files, asked for only once such a file turns up.</param>
    private sealed record InputFileRules(FolderFileSelection? Selection, DataSourceEmbeddingManifest? Manifest, Lazy<MediaTranscriptionAvailability> MediaTranscription);

    /// <inheritdoc />
    public bool Supports(IDataSourceBase dataSource) => dataSource is DataSourceLocalDirectory or DataSourceLocalFile;

    /// <inheritdoc />
    public RunShare Share => new(RunShare.DEFAULT_DURATION);

    /// <inheritdoc />
    public async Task<IndexedRunOutcome> ProcessAsync(IndexedRunContext context, DataSourceEmbeddingRefreshMode refreshMode, CancellationToken token)
    {
        if (context.DataSource is not IDataSource dataSource || !this.Supports(dataSource))
            throw new ArgumentException("The file indexer reads local files and folders only.", nameof(context));

        var manifest = context.Manifest;

        if (!await this.RelocateStoredFilesAsync(context, dataSource, token))
            return await DocumentRunProgress.FromStoredState(context, manifest.Files.Count, logger).PauseRunAsync(null, "file relocation paused", token);

        var enumerationStartedUtc = DateTimeOffset.UtcNow;
        var inputFiles = this.GetInputFiles(dataSource, manifest);
        var enumerationDuration = DateTimeOffset.UtcNow - enumerationStartedUtc;
        if (inputFiles.UnreachableRootReason is { } unreachableRootReason)
            return this.StopWithoutRoot(context, dataSource, unreachableRootReason);

        // An unmounted volume can leave a readable, empty mount directory. There is no way to
        // distinguish that from intentionally removing every file, so keep the stored index.
        // Explicit exclusions still take effect: they are evidence of a nonempty folder.
        if (dataSource is DataSourceLocalDirectory directory
            && (manifest.Files.Count > 0 || manifest.UnfinishedFiles.Count > 0 || manifest.PermanentFailures.Count > 0)
            && inputFiles.Files.Count == 0 && inputFiles.IgnoredFiles.Count == 0
            && inputFiles.FilePatternExclusionCount == 0 && inputFiles.ExcludedFolderCount == 0
            && inputFiles.FailedFiles == 0 && inputFiles.UnreadableDirectories.Count == 0)
            return this.StopWithoutRoot(context, dataSource, string.Format(TB("The folder '{0}' currently appears empty. AI Studio kept the index of this data source, because the folder of a drive that is not connected can look empty. Connect the drive and refresh this data source. If you emptied the folder on purpose, remove this data source."), directory.Path));

        var indexedFiles = inputFiles.Files;
        var totalFiles = indexedFiles.Count + inputFiles.FailedFiles + inputFiles.IgnoredFiles.Count;

        foreach (var failure in inputFiles.Failures)
        {
            logger.LogWarning(
                "Cannot index data source input '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}). Reason='{Reason}'.",
                failure.DocumentKey,
                dataSource.Name,
                dataSource.Id,
                failure.Reason);
        }

        //
        // Listing the files is what every step of a large folder pays before it reads anything,
        // hence the time it took:
        //
        logger.LogInformation(
            "Prepared data source '{DataSourceName}' ({DataSourceId}) for embedding. AccessibleFiles={AccessibleFiles}, SkippedBeforeExtraction={SkippedBeforeExtraction}, ExcludedByFilePattern={ExcludedByFilePattern}, ExcludedFolders={ExcludedFolders}, FailedFiles={FailedFiles}, EnumerationMs={EnumerationMs:F0}, Collection='{CollectionName}'.",
            dataSource.Name,
            dataSource.Id,
            indexedFiles.Count,
            inputFiles.IgnoredFiles.Count,
            inputFiles.FilePatternExclusionCount,
            inputFiles.ExcludedFolderCount,
            inputFiles.FailedFiles,
            enumerationDuration.TotalMilliseconds,
            context.CollectionName);

        var metadataSnapshot = this.BuildDataSourceMetadataSnapshot(dataSource, indexedFiles);
        var removedMissingFiles = await this.RemoveMissingFileEmbeddingsAsync(context, inputFiles, token);
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

        var progress = new DocumentRunProgress(context, totalFiles, inputFiles.FailedFiles, inputFiles.LastError, inputFiles.Failures, logger, inputFiles.IgnoredFiles, inputFiles.FilePatternExclusions, inputFiles.ExcludedFolders);
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
            this.CleanUpTranscripts(context, progress);
            return IndexedRunOutcome.DONE;
        }

        token.ThrowIfCancellationRequested();

        //
        // Everything which needs no reading is counted before the first file is read. Counted one
        // by one, the progress of every step would start at zero, and climb through the files the
        // steps before already did.
        //
        var partition = PartitionFiles(indexedFiles, metadataSnapshot.FileHashes, manifest, context.Pass);
        progress.RecordUnchanged(partition.UnchangedCount);
        foreach (var (filePath, permanentFailure) in partition.StillUnreadable)
        {
            logger.LogDebug(
                "Skipping file '{FilePath}' for data source '{DataSourceName}' ({DataSourceId}) because reading it failed permanently before. FailureCode={FailureCode}, MetadataHashPrefix={MetadataHashPrefix}, OccurredAtUtc={OccurredAtUtc:O}.",
                filePath,
                dataSource.Name,
                dataSource.Id,
                permanentFailure.Code,
                ShortHash(permanentFailure.Fingerprint),
                permanentFailure.OccurredAtUtc);
            progress.RecordStillUnreadable(filePath, permanentFailure);
        }

        foreach (var failure in partition.FailedEarlierInPass)
            progress.RecordFailedEarlierInPass(failure);

        logger.LogInformation(
            "Sorted the files of data source '{DataSourceName}' ({DataSourceId}). Unchanged={UnchangedFiles}, PermanentlySkipped={PermanentlySkippedFiles}, FailedEarlierInPass={FailedEarlierInPass}, ToRead={FilesToRead}.",
            dataSource.Name,
            dataSource.Id,
            partition.UnchangedCount,
            partition.StillUnreadable.Count,
            partition.FailedEarlierInPass.Count,
            partition.ToRead.Count);
        progress.Publish();

        for (var fileIndex = 0; fileIndex < partition.ToRead.Count; fileIndex++)
        {
            token.ThrowIfCancellationRequested();
            if (progress.HasDoneItsShare(DateTimeOffset.UtcNow))
                return await this.PauseAsync(dataSource, progress, refreshMode, partition.ToRead.Count - fileIndex, token);

            var file = partition.ToRead[fileIndex];
            var fingerprint = metadataSnapshot.FileHashes[file.FullName];
            var existingRecord = manifest.Files.GetValueOrDefault(file.FullName);

            var document = this.CreateFileDocument(context, dataSource, file, fingerprint, null);
            var reportBlockProgress = progress.BeginDocument(document);
            var startedAtUtc = DateTimeOffset.UtcNow;

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

                //
                // Audio and video hold no text until they are transcribed. The document then reads
                // its transcript instead, and its row knows which transcript that is:
                //
                if (PersistentMediaTranscript.IsMediaPath(file.FullName))
                    document = this.CreateFileDocument(context, dataSource, file, fingerprint, await this.ReadOrCreateTranscriptAsync(dataSource, file.FullName, token));

                var chunkCount = await context.IndexDocumentAsync(document, reportBlockProgress, token);
                token.ThrowIfCancellationRequested();
                var fingerprintAfterEmbedding = BuildFileMetadataHash(file);
                if (!string.Equals(fingerprint, fingerprintAfterEmbedding, StringComparison.Ordinal))
                    throw new IOException(string.Format(TB("The file '{0}' changed while it was being indexed. What was indexed of it is discarded, and the file is tried again during the next run."), file.FullName));

                await progress.RecordDocumentIndexedAsync(document, chunkCount, existingRecord is null, token);

                // The recording changed, so its old transcript is no longer what it says:
                if (existingRecord?.TranscriptKey is { } previousTranscriptKey && !string.Equals(previousTranscriptKey, document.State.TranscriptKey, StringComparison.Ordinal))
                    ForgetTranscript(DataSourceTranscriptStore.TryForDataSource(dataSource.Id), manifest, previousTranscriptKey, logger);

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

            //
            // A file is read to its end, however long that takes: its text arrives as a stream from
            // the runtime, which cannot pick up where it stopped. Every other data source waits
            // for it, so it should at least show up in the log:
            //
            var fileDuration = DateTimeOffset.UtcNow - startedAtUtc;
            if (fileDuration > context.Share.MaxDuration)
                logger.LogWarning(
                    "Reading file '{FilePath}' of data source '{DataSourceName}' ({DataSourceId}) took {FileMinutes:F0} minutes, longer than the share of a whole run, which is {ShareMinutes:F0} minutes. Every other data source waited for it.",
                    file.FullName,
                    dataSource.Name,
                    dataSource.Id,
                    fileDuration.TotalMinutes,
                    context.Share.MaxDuration.TotalMinutes);
        }

        await progress.CompleteRunAsync(metadataSnapshot.SourceHash, "data source embedding run finished", token);
        this.CleanUpTranscripts(context, progress);
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
    /// Sorts the files of a data source by what one run does with them.
    /// </summary>
    /// <remarks>
    /// A file which failed for a reason of its own is not read again until it changes. Without
    /// that, a folder holding hundreds of scanned documents without a text layer would spend half
    /// an hour on every start to arrive at the result we already have. A file which failed during
    /// an earlier step of the pass waits for the next pass, unless it changed since, cf. IndexingPass.
    /// </remarks>
    /// <param name="files">The files of the data source, in the order they are read.</param>
    /// <param name="fingerprints">The fingerprint of every one of them, by its full path.</param>
    /// <param name="manifest">What the index stores about the data source.</param>
    /// <param name="pass">The pass the run is a step of.</param>
    /// <returns>The files, sorted.</returns>
    internal static FilePartition PartitionFiles(IReadOnlyList<FileInfo> files, IReadOnlyDictionary<string, string> fingerprints, DataSourceEmbeddingManifest manifest, IndexingPass pass)
    {
        var unchangedCount = 0;
        var stillUnreadable = new List<KeyValuePair<string, PermanentIndexingFailureRecord>>();
        var failedEarlierInPass = new List<DataSourceEmbeddingFailure>();
        var toRead = new List<FileInfo>();

        foreach (var file in files)
        {
            var fingerprint = fingerprints[file.FullName];
            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord) && string.Equals(existingRecord.Fingerprint, fingerprint, StringComparison.Ordinal))
                unchangedCount++;
            else if (manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure) && string.Equals(permanentFailure.Fingerprint, fingerprint, StringComparison.Ordinal))
                stillUnreadable.Add(new(file.FullName, permanentFailure));
            else if (pass.TryGetEarlierFailure(file.FullName, fingerprint, out var earlierFailure))
                failedEarlierInPass.Add(earlierFailure);
            else
                toRead.Add(file);
        }

        return new(unchangedCount, stillUnreadable, failedEarlierInPass, toRead);
    }

    /// <summary>
    /// Ends a run which did its share of the work before it read every file.
    /// </summary>
    /// <remarks>
    /// Without a hash, cf. DocumentRunProgress.PauseRunAsync: a folder which is indexed for the
    /// first time must not count as complete, and one which was complete before keeps the hash of
    /// back then, which no longer matches, so the next run reads on.
    /// </remarks>
    private async Task<IndexedRunOutcome> PauseAsync(IDataSource dataSource, DocumentRunProgress progress, DataSourceEmbeddingRefreshMode refreshMode, int filesLeftToRead, CancellationToken token)
    {
        var outcome = await progress.PauseRunAsync(null, "data source embedding run paused", token);
        if (outcome is not IndexedRunOutcome.MORE_TO_DO)
            return outcome;

        logger.LogInformation(
            "Embedding data source '{DataSourceName}' ({DataSourceId}) carries on in another run. RefreshMode={RefreshMode}, Embedded={EmbeddedFiles}, Failed={FailedFiles}, Done={DoneFiles}/{TotalFiles}, LeftToRead={FilesLeftToRead}.",
            dataSource.Name,
            dataSource.Id,
            refreshMode,
            progress.IndexedDocuments,
            progress.FailedDocuments,
            progress.DoneDocuments,
            progress.TotalDocuments,
            filesLeftToRead);

        return IndexedRunOutcome.MORE_TO_DO;
    }

    /// <summary>
    /// Ends a run which cannot reach the file or the folder of its data source, and keeps the index as it is.
    /// </summary>
    /// <remarks>
    /// A folder out of reach is never the same as an empty one. Read that way, a drive which is not
    /// connected, or a network share while the VPN is down, would remove every embedding of the
    /// data source, and embedding it all again once the folder is back costs as long as it took the
    /// first time. Nothing is written: the stored hash still tells which state of the data source
    /// the index holds, so the next run picks up from there.
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="dataSource">The data source.</param>
    /// <param name="reason">Why the file or the folder cannot be reached, and what the user can do about it.</param>
    /// <returns>DONE, since nothing can be done until the file or the folder is back.</returns>
    private IndexedRunOutcome StopWithoutRoot(IndexedRunContext context, IDataSource dataSource, string reason)
    {
        logger.LogWarning(
            "Cannot reach the file or folder of data source '{DataSourceName}' ({DataSourceId}). The run stops, and nothing is removed from the index. StoredFiles={StoredFiles}.",
            dataSource.Name,
            dataSource.Id,
            context.Manifest.Files.Count);

        DocumentRunProgress.FromStoredState(context, context.Manifest.Files.Count, logger).PublishRunFailure(reason);
        return IndexedRunOutcome.DONE;
    }

    /// <summary>
    /// Deletes the transcripts which no file of the data source uses anymore, once a run got through all of it.
    /// </summary>
    /// <remarks>
    /// Only a run without a failure knows every transcript which is still needed: a file which
    /// failed may have its transcript already, and its next attempt must not pay for it again.
    /// The transcript of a recording which is gone has to go as well, since it holds what was said.
    /// </remarks>
    /// <param name="context">The run.</param>
    /// <param name="progress">How the run went.</param>
    private void CleanUpTranscripts(IndexedRunContext context, DocumentRunProgress progress)
    {
        if (progress.FailedDocuments > 0)
            return;

        DeleteUnusedTranscripts(DataSourceTranscriptStore.TryForDataSource(context.DataSource.Id), context.Manifest, logger);
    }

    /// <summary>
    /// Deletes every transcript of a data source which no indexed file uses, and the temporary files of interrupted writes.
    /// </summary>
    /// <param name="store">The transcripts of the data source, or null when there are none to clean up.</param>
    /// <param name="manifest">What the index stores about the data source.</param>
    /// <param name="logger">The logger.</param>
    internal static void DeleteUnusedTranscripts(DataSourceTranscriptStore? store, DataSourceEmbeddingManifest manifest, ILogger logger)
    {
        if (store is null)
            return;

        try
        {
            var usedKeys = manifest.Files.Values
                .Select(record => record.TranscriptKey)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            var deleted = store.ListKeys().Count(key => !usedKeys.Contains(key) && store.Delete(key));
            deleted += store.DeleteTemporaryFiles();
            if (deleted > 0)
                logger.LogInformation("Removed {DeletedTranscripts} transcript file(s) which no file of the data source uses anymore.", deleted);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the transcripts which no file of the data source uses anymore. The next complete run tries again.");
        }
    }

    /// <summary>
    /// Deletes the transcript with the given key, unless another file of the data source still uses it.
    /// </summary>
    /// <remarks>
    /// Two recordings with the same content share one transcript, cf. DataSourceTranscriptStore.
    /// </remarks>
    /// <param name="store">The transcripts of the data source, or null when there are none to clean up.</param>
    /// <param name="manifest">What the index stores about the data source, without the file the transcript belonged to.</param>
    /// <param name="key">The key of the transcript, or null when the file had none.</param>
    /// <param name="logger">The logger.</param>
    internal static void ForgetTranscript(DataSourceTranscriptStore? store, DataSourceEmbeddingManifest manifest, string? key, ILogger logger)
    {
        if (store is null || string.IsNullOrWhiteSpace(key) || manifest.Files.Values.Any(record => string.Equals(record.TranscriptKey, key, StringComparison.Ordinal)))
            return;

        try
        {
            if (store.Delete(key))
                logger.LogInformation("Removed the transcript '{TranscriptKey}', since no file of the data source uses it anymore.", key);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not remove the transcript '{TranscriptKey}'. The next complete run tries again.", key);
        }
    }

    private async Task<int> RemoveMissingFileEmbeddingsAsync(IndexedRunContext context, FileEnumerationResult inputFiles, CancellationToken token)
    {
        var manifest = context.Manifest;
        var removedFiles = 0;
        foreach (var removedFilePath in GetGoneFiles(manifest.Files.Keys, inputFiles.Files, inputFiles.UnreadableDirectories))
        {
            await context.DeleteDocumentPointsAsync(removedFilePath, token);
            await context.IndexStore.DeleteFileAsync(context.DataSource.Id, removedFilePath, token);
            var removedTranscriptKey = manifest.Files.GetValueOrDefault(removedFilePath)?.TranscriptKey;
            manifest.Files.Remove(removedFilePath);
            ForgetTranscript(DataSourceTranscriptStore.TryForDataSource(context.DataSource.Id), manifest, removedTranscriptKey, logger);
            removedFiles++;
            logger.LogInformation(
                "Removed stale embeddings for deleted file '{FilePath}' from data source '{DataSourceName}' ({DataSourceId}).",
                removedFilePath,
                context.DataSource.Name,
                context.DataSource.Id);
        }

        //
        // A file whose indexing never came to an end, e.g., since its run was canceled or the app
        // was closed meanwhile, left a row without chunks and perhaps some vectors. Indexing it again
        // would replace both, but a file which is gone or no longer selected is never indexed again:
        //
        foreach (var unfinishedFilePath in GetGoneFiles(manifest.UnfinishedFiles, inputFiles.Files, inputFiles.UnreadableDirectories))
        {
            await context.DeleteDocumentPointsAsync(unfinishedFilePath, token);
            await context.IndexStore.DeleteFileAsync(context.DataSource.Id, unfinishedFilePath, token);
            manifest.UnfinishedFiles.Remove(unfinishedFilePath);
            logger.LogInformation(
                "Removed what the unfinished indexing of file '{FilePath}' left behind in data source '{DataSourceName}' ({DataSourceId}), since the file is gone or no longer selected.",
                unfinishedFilePath,
                context.DataSource.Name,
                context.DataSource.Id);
        }

        //
        // A file which is gone needs no mark keeping it out of the index. Without this, the table
        // would grow with every document the user ever deleted:
        //
        foreach (var removedFilePath in GetGoneFiles(manifest.PermanentFailures.Keys, inputFiles.Files, inputFiles.UnreadableDirectories))
            await context.ForgetPermanentFailureAsync(removedFilePath, token);

        return removedFiles;
    }

    /// <summary>
    /// Picks the files which are gone from the data source, out of those the index knows about.
    /// </summary>
    /// <remarks>
    /// A file below a folder which could not be listed is not gone, only out of reach, e.g. while the
    /// user may not read that folder. Removing it would throw away its index, and embedding it again
    /// once the folder can be read costs as long as it took the first time.
    /// <br /><br />
    /// Paths are compared ignoring their case, as everywhere in the index, cf. the remarks on
    /// DataSourceEmbeddingManifest.PATH_COMPARER. On Linux and macOS, that keeps a file whose path
    /// differs from an unreadable folder in its case alone, which costs no more than a stale entry
    /// until the folder can be read again.
    /// </remarks>
    /// <param name="storedPaths">The paths of the files the index knows about.</param>
    /// <param name="files">The files this run found.</param>
    /// <param name="unreadableDirectories">The folders this run could not list.</param>
    /// <returns>The paths of the files which are gone.</returns>
    internal static List<string> GetGoneFiles(IEnumerable<string> storedPaths, IEnumerable<FileInfo> files, IEnumerable<string> unreadableDirectories)
    {
        var existingPaths = files
            .Select(file => file.FullName)
            .ToHashSet(DataSourceEmbeddingManifest.PATH_COMPARER);

        var unreadablePrefixes = unreadableDirectories
            .Select(Path.GetFullPath)
            .Select(directory => Path.EndsInDirectorySeparator(directory) ? directory : directory + Path.DirectorySeparatorChar)
            .ToList();

        return storedPaths
            .Where(path => !existingPaths.Contains(path))
            .Where(path => !unreadablePrefixes.Any(prefix => path.StartsWith(prefix, DataSourceEmbeddingManifest.PATH_COMPARISON)))
            .ToList();
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

    private async IAsyncEnumerable<EmbeddingChunk> StreamEmbeddingChunksAsync(string filePath, MediaTranscriptSlot? transcript, IDataSource dataSource, EmbeddingProvider embeddingProvider, PromptInjectionReportingScope reportingScope, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        var options = DataSourceEmbeddingService.GetChunkingOptions(dataSource, embeddingProvider);

        //
        // An audio or video file is read through its transcript. The runtime reads the transcript
        // like every other document, so it passes the same prompt injection filter, and a notice
        // names the media file rather than the transcript below the data directory:
        //
        var contentPath = transcript?.Path ?? filePath;
        PromptInjectionSource? reportAs = transcript is null ? null : PromptInjectionSource.FileContent(filePath);
        var strategy = TextChunker.GetStrategyForFile(contentPath);
        var content = await ExtractedFileText.ReadAsync(rustService, contentPath, embeddingProvider, reportAs, reportingScope, token);

        await foreach (var chunk in textChunker.SplitAsync(content, strategy, options, embeddingProvider, token))
            yield return chunk;
    }

    /// <summary>
    /// Returns the transcript of an audio or video file, transcribing the file only when the data source holds no transcript of its content yet.
    /// </summary>
    /// <remarks>
    /// The provider is checked against the data source once more, right before the upload: the
    /// settings may have changed since the files were listed. A transcript which exists is used
    /// without any provider, since nothing leaves the computer for it.<br/><br/>
    ///
    /// A transcription which cannot succeed for this file, such as one without text, is recorded
    /// for good, so the file is not paid for again until it changes.
    /// </remarks>
    /// <param name="dataSource">The data source.</param>
    /// <param name="mediaPath">The audio or video file.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>Where the transcript is.</returns>
    private async Task<MediaTranscriptSlot> ReadOrCreateTranscriptAsync(IDataSource dataSource, string mediaPath, CancellationToken token)
    {
        var provider = this.GetMediaTranscriptionAvailability(dataSource).Provider;
        var transcript = await PersistentMediaTranscript.ReadOrCreateAsync(DataSourceTranscriptStore.ForDataSource(dataSource.Id), mediaPath, provider, mediaTranscriptionService.TranscribeAsync, logger, token);
        return GetTranscriptOrThrow(transcript, token);
    }

    /// <summary>
    /// Returns the transcript, or throws what tells the run how to treat the media file without one.
    /// </summary>
    /// <remarks>
    /// A FileExtractionException records the file for good, until it changes; any other exception
    /// lets the next pass try again. A transcription without text, a file which the media pipeline
    /// cannot read, and a file which the transcription provider refused would fail the same way
    /// every time, and every attempt would cost money or another upload.
    /// </remarks>
    /// <param name="transcript">The outcome of the request for the transcript.</param>
    /// <param name="token">The cancellation token of the run.</param>
    /// <returns>Where the transcript is.</returns>
    internal static MediaTranscriptSlot GetTranscriptOrThrow(PersistentMediaTranscriptResult transcript, CancellationToken token) => transcript.Status switch
    {
        PersistentMediaTranscriptStatus.READY => new(transcript.TranscriptPath, transcript.Key),
        PersistentMediaTranscriptStatus.CANCELLED => throw new OperationCanceledException(token),
        PersistentMediaTranscriptStatus.EMPTY or PersistentMediaTranscriptStatus.NO_AUDIBLE_SIGNAL => throw new FileExtractionException(FileExtractionErrorCode.NO_CONTENT, transcript.UserMessage),
        PersistentMediaTranscriptStatus.TRANSCRIPTION_FAILED when IsPermanentMediaFailure(transcript.ErrorCode) => throw new FileExtractionException(FileExtractionErrorCode.MEDIA_PROCESSING_FAILED, transcript.UserMessage),
        PersistentMediaTranscriptStatus.REJECTED => throw new FileExtractionException(FileExtractionErrorCode.MEDIA_PROCESSING_FAILED, transcript.UserMessage),
        _ => throw new IOException(transcript.UserMessage),
    };

    /// <summary>
    /// Which transcription provider may hear the audio and video files of a data source right now.
    /// </summary>
    /// <remarks>
    /// The transcription provider receives the content of the data source just like the embedding
    /// provider, so it has to meet the confidence level of the data source as well, cf.
    /// DataSourceEmbeddingService.AllowsTranscription. A data source without a confidence level
    /// of its own is never sent anywhere.
    /// </remarks>
    /// <param name="dataSource">The data source.</param>
    /// <returns>The provider, or why there is none.</returns>
    private MediaTranscriptionAvailability GetMediaTranscriptionAvailability(IDataSource dataSource)
    {
        var provider = mediaTranscriptionService.GetUsableProvider();
        if (provider is null)
            return MediaTranscriptionAvailability.MISSING;

        var providerConfidence = provider.GetConfidenceLevel(settingsManager);
        if (dataSource is IIndexedDataSource indexedDataSource && DataSourceEmbeddingService.AllowsTranscription(indexedDataSource, providerConfidence))
            return MediaTranscriptionAvailability.Allowed(provider);

        logger.LogInformation(
            "The transcription provider '{ProviderName}' may not hear the audio and video files of data source '{DataSourceName}' ({DataSourceId}): the data source asks for the confidence level {RequiredConfidence}, while the provider has {ProviderConfidence}.",
            provider.Name,
            dataSource.Name,
            dataSource.Id,
            (dataSource as IIndexedDataSource)?.ConfidenceLevel,
            providerConfidence);

        return MediaTranscriptionAvailability.NOT_ALLOWED;
    }

    private static bool IsPermanentMediaFailure(MediaJobErrorCode? code) => code is
        MediaJobErrorCode.UNKNOWN_FORMAT
        or MediaJobErrorCode.UNSAFE_FILE
        or MediaJobErrorCode.NOT_MEDIA
        or MediaJobErrorCode.UNSUPPORTED_CONTAINER
        or MediaJobErrorCode.NO_AUDIO_TRACK
        or MediaJobErrorCode.UNSUPPORTED_CODEC
        or MediaJobErrorCode.INVALID_AUDIO_PARAMETERS
        or MediaJobErrorCode.INVALID_OPUS_HEADER
        or MediaJobErrorCode.UNSUPPORTED_OPUS_MAPPING
        or MediaJobErrorCode.DAMAGED_CONTAINER;

    /// <summary>
    /// Lists the files of a data source and decides what a run does with each of them.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="manifest">What the index stores about the data source, or null when it does not matter.</param>
    /// <param name="mediaTranscription">Which transcription provider may hear its audio and video files, or null to ask the settings once a media file turns up.</param>
    /// <returns>The files, and those which were left out.</returns>
    internal FileEnumerationResult GetInputFiles(IDataSource dataSource, DataSourceEmbeddingManifest? manifest = null, MediaTranscriptionAvailability? mediaTranscription = null)
    {
        var result = new FileEnumerationResult();
        var mediaTranscriptionAvailability = new Lazy<MediaTranscriptionAvailability>(() => mediaTranscription ?? this.GetMediaTranscriptionAvailability(dataSource));

        switch (dataSource)
        {
            case DataSourceLocalFile localFile when RelocatedFilePaths.IsLocalAbsolute(localFile.FilePath) && File.Exists(localFile.FilePath):
                var file = new FileInfo(localFile.FilePath);

                // No size limit: the user chose this very file, cf. FolderFileSelection.IsTooLarge.
                if (this.AddInputFile(file, new InputFileRules(null, manifest, mediaTranscriptionAvailability), result) is RagFileIndexingDecision.EXCLUDED)
                    logger.LogDebug("Skipping excluded file '{FilePath}' while indexing.", file.FullName);

                return result;

            case DataSourceLocalDirectory localDirectory when RelocatedFilePaths.IsLocalAbsolute(localDirectory.Path) && Directory.Exists(localDirectory.Path):
                this.EnumerateAccessibleFiles(localDirectory, manifest, mediaTranscriptionAvailability, result);
                if (result.UnreadableDirectories.Any(directory => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)),
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(localDirectory.Path)), PathTools.COMPARISON)))
                    result.UnreachableRootReason = string.Format(TB("The folder '{0}' could not be opened. Please check whether you are allowed to read it. Nothing was removed from the index."), localDirectory.Path);

                return result;
        }

        //
        // Missing is not the same as empty, cf. StopWithoutRoot:
        //
        switch (dataSource)
        {
            case DataSourceLocalFile localFile:
                result.UnreachableRootReason = string.Format(TB("The file '{0}' cannot be reached. Perhaps the drive it is on is not connected, or the file was moved or renamed. Nothing was removed from the index. If the file was moved or renamed, edit this data source to select its new location. Once the file is back, refresh this data source. When it is gone for good, remove this data source."), localFile.FilePath);
                break;

            case DataSourceLocalDirectory localDirectory:
                result.UnreachableRootReason = string.Format(TB("The folder '{0}' cannot be reached. Perhaps the drive it is on is not connected, or the folder was moved or renamed. Nothing was removed from the index. If the folder was moved or renamed, edit this data source to select its new location. Once the folder is back, refresh this data source."), localDirectory.Path);
                break;
        }

        return result;
    }

    private void EnumerateAccessibleFiles(DataSourceLocalDirectory dataSource, DataSourceEmbeddingManifest? manifest, Lazy<MediaTranscriptionAvailability> mediaTranscription, FileEnumerationResult result)
    {
        var selection = FolderFileSelection.Of(dataSource);
        var rules = new InputFileRules(selection, manifest, mediaTranscription);

        //
        // Every log line travels to the runtime as a request of its own. A folder holding many
        // temporary or system files gets one line in total rather than one per file:
        //
        var excludedFiles = 0;
        var excludedFileExamples = new List<string>(MAX_LOGGED_EXCLUDED_FILE_EXAMPLES);

        foreach (var filePath in selection.EnumerateCandidates(
                     dataSource.Path,
                     reportExcludedFolder: result.AddExcludedFolder,
                     reportInaccessibleDirectory: (directoryPath, exception) =>
                     {
                         logger.LogWarning(exception, "Cannot access directory '{DirectoryPath}' while indexing.", directoryPath);
                         result.AddUnreadableDirectory(directoryPath, string.Format(TB("The folder '{0}' could not be opened. Please check whether you are allowed to read it."), directoryPath));
                     }))
        {
            if (!selection.Selects(filePath))
            {
                // Everything in a folder the user chose to scan is useful transparency when a
                // pattern was mistyped or was narrower than intended, or when the exclusion list
                // caught a file somebody wanted:
                result.AddFilePatternExclusion(filePath);
                continue;
            }

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

            //
            // The transcript which the Batch Processing assistant wrote next to a selected media
            // file holds the text the data source gets from the media file itself:
            //
            if (MediaSidecarTranscripts.IsCoveredArtifact(fileInfo.FullName, selection))
            {
                logger.LogDebug("Skipping the transcript '{FilePath}', because its media file is indexed as the document.", fileInfo.FullName);
                continue;
            }

            if (this.AddInputFile(fileInfo, rules, result) is not RagFileIndexingDecision.EXCLUDED)
                continue;

            excludedFiles++;
            if (excludedFileExamples.Count < MAX_LOGGED_EXCLUDED_FILE_EXAMPLES)
                excludedFileExamples.Add(Path.GetRelativePath(dataSource.Path, fileInfo.FullName));
        }

        if (excludedFiles > 0)
            logger.LogDebug("Skipped {ExcludedFiles} excluded file(s) in '{RootPath}' while indexing, for example: {ExampleFiles}.", excludedFiles, dataSource.Path, string.Join(", ", excludedFileExamples.Select(path => $"'{path}'")));
    }

    /// <param name="file">The file.</param>
    /// <param name="rules">What decides about the files of the data source.</param>
    /// <param name="result">Where the file goes, if anywhere.</param>
    private RagFileIndexingDecision AddInputFile(FileInfo file, InputFileRules rules, FileEnumerationResult result)
    {
        var decision = this.GetRagFileIndexingDecision(file, rules);
        switch (decision)
        {
            case RagFileIndexingDecision.INDEXABLE:
                result.Files.Add(file);
                break;

            case RagFileIndexingDecision.EXCLUDED:
                break;

            case RagFileIndexingDecision.IMAGE_NOT_SUPPORTED:
                result.AddIgnoredFile(file.FullName, DataSourceEmbeddingIgnoreReason.IMAGE_NOT_SUPPORTED);
                break;

            case RagFileIndexingDecision.TRANSCRIPTION_PROVIDER_MISSING:
                result.AddIgnoredFile(file.FullName, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING);
                break;

            case RagFileIndexingDecision.TRANSCRIPTION_PROVIDER_NOT_ALLOWED:
                result.AddIgnoredFile(file.FullName, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_NOT_ALLOWED);
                break;

            case RagFileIndexingDecision.TOO_LARGE:
                result.AddIgnoredFile(file.FullName, DataSourceEmbeddingIgnoreReason.TOO_LARGE);
                break;
        }

        return decision;
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

    private RagFileIndexingDecision GetRagFileIndexingDecision(FileInfo file, InputFileRules rules)
    {
        if (this.IsSkippedRagFile(file))
            return RagFileIndexingDecision.EXCLUDED;

        if (!IMAGE_EMBEDDING_ENABLED && this.IsImageFilePath(file.FullName))
            return RagFileIndexingDecision.IMAGE_NOT_SUPPORTED;

        //
        // Before any transcription: a video of several gigabytes is left out like every other
        // file above the limit, rather than sent to the transcription provider.
        //
        if (rules.Selection?.IsTooLarge(file) is true)
            return RagFileIndexingDecision.TOO_LARGE;

        if (PersistentMediaTranscript.IsMediaPath(file.FullName))
            return GetMediaIndexingDecision(file, rules);

        // A directory source reaches this point only after its user-configured pattern selected
        // the file. A single-file source was validated before it was stored. Extraction is the
        // authoritative capability check and records an unreadable selected file through the
        // ordinary per-document failure path instead of silently overriding the user's selection.
        return RagFileIndexingDecision.INDEXABLE;
    }

    /// <summary>
    /// Decides about an audio or video file, which holds no text until it is transcribed.
    /// </summary>
    /// <remarks>
    /// A media file which the index holds unchanged needs no transcript in this run, nor does one
    /// which failed for good before: neither is read. A missing transcription provider, or one which
    /// the data source no longer allows, therefore takes nothing out of the index. Every other media
    /// file needs a provider which may hear it, unless the data source has its transcript already;
    /// that is only known once the file is read, so such a file waits for the provider as well.
    /// </remarks>
    /// <param name="file">The audio or video file.</param>
    /// <param name="rules">What decides about the files of the data source.</param>
    /// <returns>INDEXABLE, or why the file is left out.</returns>
    private static RagFileIndexingDecision GetMediaIndexingDecision(FileInfo file, InputFileRules rules)
    {
        if (rules.Manifest is { } manifest)
        {
            var fingerprint = BuildFileMetadataHash(file);
            if (manifest.Files.TryGetValue(file.FullName, out var existingRecord) && string.Equals(existingRecord.Fingerprint, fingerprint, StringComparison.Ordinal))
                return RagFileIndexingDecision.INDEXABLE;

            if (manifest.PermanentFailures.TryGetValue(file.FullName, out var permanentFailure) && string.Equals(permanentFailure.Fingerprint, fingerprint, StringComparison.Ordinal))
                return RagFileIndexingDecision.INDEXABLE;
        }

        return rules.MediaTranscription.Value.IgnoreReason switch
        {
            null => RagFileIndexingDecision.INDEXABLE,
            DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_NOT_ALLOWED => RagFileIndexingDecision.TRANSCRIPTION_PROVIDER_NOT_ALLOWED,
            _ => RagFileIndexingDecision.TRANSCRIPTION_PROVIDER_MISSING,
        };
    }

    private bool IsSkippedRagFile(FileInfo file)
    {
        if (FileExclusions.IsAlwaysExcludedFile(file.Name))
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

    private DataSourceMetadataSnapshot BuildDataSourceMetadataSnapshot(IDataSource dataSource, IReadOnlyList<FileInfo> indexedFiles)
    {
        var fileHashes = indexedFiles
            .OrderBy(file => file.FullName, DataSourceEmbeddingManifest.PATH_COMPARER)
            .ToDictionary(file => file.FullName, BuildFileMetadataHash, DataSourceEmbeddingManifest.PATH_COMPARER);

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

        foreach (var file in indexedFiles.OrderBy(file => file.FullName, DataSourceEmbeddingManifest.PATH_COMPARER))
        {
            parts.Add(this.TryGetRelativePath(dataSource, file));
            parts.Add(fileHashes[file.FullName]);
        }

        return BuildMetadataHash(parts);
    }

    internal static string BuildFileMetadataHash(FileInfo file)
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

        return BuildFileMetadataHash(file.FullName, file.Name, file.LastWriteTimeUtc, file.Length);
    }

    /// <summary>
    /// The fingerprint of a file which exists, from its write time and size as they were read.
    /// </summary>
    /// <remarks>
    /// The one formula for both an indexed file and a relocated one: if the two drifted apart, every
    /// relocated file would look changed to the next run and be embedded again.
    /// </remarks>
    /// <param name="fullName">The absolute path of the file.</param>
    /// <param name="name">The name of the file.</param>
    /// <param name="lastWriteUtc">When the file was last written to.</param>
    /// <param name="length">The size of the file in bytes.</param>
    /// <returns>The fingerprint.</returns>
    private static string BuildFileMetadataHash(string fullName, string name, DateTime lastWriteUtc, long length) =>
        BuildMetadataHash("file", fullName, name, lastWriteUtc.Ticks.ToString(), length.ToString());

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
    /// <param name="transcript">The transcript an audio or video file is read through, or null for every other file.</param>
    /// <returns>The document.</returns>
    private EmbeddingDocument CreateFileDocument(IndexedRunContext context, IDataSource dataSource, FileInfo file, string fingerprint, MediaTranscriptSlot? transcript) => new(
        file.FullName,
        this.CreateEmbeddingStateFile(dataSource, file, fingerprint, 0, DateTimeOffset.UtcNow) with { ParentFileId = context.GetFileParentId(file.FullName), TranscriptKey = transcript?.Key },
        file.Name,
        token => this.StreamEmbeddingChunksAsync(file.FullName, transcript, dataSource, context.EmbeddingProvider, context.PromptInjectionScope, token));

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
