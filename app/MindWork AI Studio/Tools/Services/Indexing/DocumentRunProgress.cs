using AIStudio.Provider;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.PluginSystem;

using static AIStudio.Tools.Services.Indexing.IndexingLogFormat;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How far an indexing run got through the documents of its data source, and what became of each.
/// </summary>
/// <remarks>
/// Counts the documents, keeps the list of failures and turns both into the status the embedding
/// page shows. What happens to a document after it was indexed, or after it failed, is the same for
/// every kind of data source, so that is decided here as well: the stores learn about it, the
/// manifest follows, and the user hears about it the way the failure deserves.
/// </remarks>
/// <param name="context">The run the documents are indexed in.</param>
/// <param name="totalDocuments">How many documents the data source has, readable or not.</param>
/// <param name="failedInputs">How many of them could not even be looked at.</param>
/// <param name="lastInputError">Why the last of those could not be looked at, or an empty string.</param>
/// <param name="inputFailures">The failures of those which could not be looked at.</param>
/// <param name="logger">The logger of the embedding service, so the log reads the same whoever writes it.</param>
internal sealed class DocumentRunProgress(IndexedRunContext context, int totalDocuments, int failedInputs, string lastInputError, IEnumerable<DataSourceEmbeddingFailure> inputFailures, ILogger logger)
{
    /// <summary>
    /// How often the block progress within one document is reported to the user interface at most.
    /// </summary>
    private static readonly TimeSpan BLOCK_PROGRESS_INTERVAL = TimeSpan.FromSeconds(3);

    private readonly List<DataSourceEmbeddingFailure> failures = inputFailures.ToList();

    //
    // Which kinds of provider failure the user was already told about in this run. A rejected
    // API key is the same problem for every one of a few thousand documents, and one message
    // is what it takes to send the user to the settings.
    //
    private readonly HashSet<ProviderRequestFailureReason> reportedFailureReasons = [];

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DocumentRunProgress).Namespace, nameof(DocumentRunProgress));

    public int TotalDocuments => totalDocuments;

    /// <summary>
    /// Documents which were left alone because nothing changed since they were indexed.
    /// </summary>
    public int UnchangedDocuments { get; private set; }

    /// <summary>
    /// Documents which were not read because they failed for a reason of their own before.
    /// </summary>
    public int PermanentlySkippedDocuments { get; private set; }

    /// <summary>
    /// Documents which were indexed in this run.
    /// </summary>
    public int IndexedDocuments { get; private set; }

    public int NewDocuments { get; private set; }

    public int ChangedDocuments { get; private set; }

    public int FailedDocuments { get; private set; } = failedInputs;

    public string LastError { get; private set; } = lastInputError;

    /// <summary>
    /// Documents which need nothing more in this run, whether they were indexed now or before.
    /// </summary>
    public int DoneDocuments => this.UnchangedDocuments + this.IndexedDocuments;

    /// <summary>
    /// Counts documents which nothing changed about since they were indexed.
    /// </summary>
    /// <param name="count">How many of them.</param>
    public void RecordUnchanged(int count = 1) => this.UnchangedDocuments += count;

    /// <summary>
    /// Counts a document which is not read again, because it failed for a reason of its own before
    /// and has not changed since.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="failure">Why it failed back then.</param>
    public void RecordStillUnreadable(string documentKey, PermanentIndexingFailureRecord failure)
    {
        this.PermanentlySkippedDocuments++;

        // The stored reason keeps its place in the list, so the user still sees why:
        this.failures.Add(new DataSourceEmbeddingFailure(documentKey, failure.Message, failure.OccurredAtUtc, ExtractionCode: failure.Code, IsPermanent: true));
    }

    /// <summary>
    /// Tells the user interface where the run stands.
    /// </summary>
    /// <param name="currentDocument">The name of the document being worked on, or an empty string.</param>
    /// <param name="currentBlock">The block of that document being worked on, when known.</param>
    /// <param name="currentPage">The page that block is on, when known.</param>
    public void Publish(string currentDocument = "", int? currentBlock = null, int? currentPage = null) =>
        context.PublishStatus(this.CreateStatus(DataSourceEmbeddingState.RUNNING, currentDocument, this.LastError, currentBlock, currentPage));

    /// <summary>
    /// Announces that work on a document starts, and hands out what reports its progress.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>Told about every block of the document, with its number and its page.</returns>
    public Action<int, int?> BeginDocument(EmbeddingDocument document)
    {
        this.Publish(document.DisplayName);

        //
        // What the page says while one document is being worked on. Without it, a document of
        // several thousand pages leaves the same sentence standing for hours, and a progress
        // which never moves cannot be told apart from one which is stuck.
        //
        var lastBlockReportUtc = DateTimeOffset.MinValue;
        return (blockNumber, pageNumber) =>
        {
            //
            // The first block goes out at once, so the line is there instead of blank. After
            // that, at most one message every BLOCK_PROGRESS_INTERVAL: each one re-renders the
            // embedding page, the navigation bar and the table in the settings, and the blocks
            // of a large document arrive far faster than anybody can read them.
            //
            var nowUtc = DateTimeOffset.UtcNow;
            if (blockNumber > 1 && nowUtc - lastBlockReportUtc < BLOCK_PROGRESS_INTERVAL)
                return;

            lastBlockReportUtc = nowUtc;
            this.Publish(document.DisplayName, blockNumber, pageNumber);
        };
    }

    /// <summary>
    /// Records a document as indexed, in the index store as well as in the manifest.
    /// </summary>
    /// <remarks>
    /// Called once the kind of data source is sure the document did not change while it was read.
    /// Until then, its index row says it has no chunks.
    /// </remarks>
    /// <param name="document">The document.</param>
    /// <param name="chunkCount">How many chunks were stored for it.</param>
    /// <param name="isNew">Whether the index knew nothing about it before.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task RecordDocumentIndexedAsync(EmbeddingDocument document, int chunkCount, bool isNew, CancellationToken token)
    {
        var embeddedAtUtc = DateTimeOffset.UtcNow;
        var state = document.State with { ChunkCount = chunkCount, EmbeddedAtUtc = embeddedAtUtc };
        await context.IndexStore.UpsertFileAsync(context.DataSource.Id, state, token);
        context.Manifest.Files[document.Key] = new EmbeddedFileRecord(state.Fingerprint, state.FileSize, state.LastWriteUtc, embeddedAtUtc, chunkCount);
        await context.ForgetPermanentFailureAsync(document.Key, token);

        this.IndexedDocuments++;
        if (isNew)
            this.NewDocuments++;
        else
            this.ChangedDocuments++;
    }

    /// <summary>
    /// Records why a document could not be indexed, and removes what the attempt left behind.
    /// </summary>
    /// <remarks>
    /// Never called for a cancelled run, nor for a vector store which cannot be read at all: those
    /// are not about one document, and whoever indexes has to let them through.
    /// </remarks>
    /// <param name="document">The document.</param>
    /// <param name="exception">What went wrong.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task RecordDocumentFailureAsync(EmbeddingDocument document, Exception exception, CancellationToken token)
    {
        var dataSource = context.DataSource;
        switch (exception)
        {
            case ProviderRequestException providerFailure:
            {
                //
                // The provider said what went wrong and what the user can do about it. That
                // sentence is what goes into the status, together with the classification the UI
                // needs to offer the matching way out.
                //
                this.FailedDocuments++;
                this.LastError = providerFailure.UserMessage;
                this.failures.Add(new DataSourceEmbeddingFailure(document.Key, providerFailure.UserMessage, DateTimeOffset.UtcNow, providerFailure.FailureReason, providerFailure.StatusCode, context.EmbeddingProvider.Name));
                context.Manifest.Files.Remove(document.Key);
                await context.ForgetPermanentFailureAsync(document.Key, token);
                await context.CleanupFailedDocumentAsync(document.Key, token);

                logger.LogWarning(
                    providerFailure,
                    "Failed to embed file '{FilePath}' for data source '{DataSourceName}' because the embedding provider '{EmbeddingProviderName}' failed. FailureReason={FailureReason}, StatusCode={StatusCode}.",
                    document.Key,
                    dataSource.Name,
                    context.EmbeddingProvider.Name,
                    providerFailure.FailureReason,
                    providerFailure.StatusCode);
                this.Publish(document.DisplayName);

                // Once per kind of failure, not once per document:
                if (this.reportedFailureReasons.Add(providerFailure.FailureReason))
                    await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, providerFailure.UserMessage));

                break;
            }

            case FileExtractionException extractionFailure when extractionFailure.Code.IsPermanentIndexingFailure():
            {
                //
                // The document itself is why this failed, so trying it again changes nothing until
                // the document does. The reason is written into the index, and the fingerprint next
                // to it decides when to come back: an OCR run over a scanned PDF changes both size
                // and write time, which is exactly the moment the file deserves another attempt.
                //
                this.PermanentlySkippedDocuments++;
                var occurredAtUtc = DateTimeOffset.UtcNow;
                var indexingMessage = extractionFailure.Code.ToIndexingUserMessage(document.DisplayName);
                this.failures.Add(new DataSourceEmbeddingFailure(document.Key, indexingMessage, occurredAtUtc, ExtractionCode: extractionFailure.Code, IsPermanent: true));
                context.Manifest.Files.Remove(document.Key);
                await context.CleanupFailedDocumentAsync(document.Key, token);

                var state = document.State;
                context.Manifest.PermanentFailures[state.AbsolutePath] = new PermanentIndexingFailureRecord(state.Fingerprint, extractionFailure.Code, indexingMessage, occurredAtUtc);
                await context.IndexStore.UpsertPermanentFailureAsync(
                    dataSource.Id,
                    new PermanentIndexingFailure(state.ParentFileId, state.AbsolutePath, state.Fingerprint, extractionFailure.Code, indexingMessage, occurredAtUtc),
                    token);

                logger.LogInformation(
                    extractionFailure,
                    "Skipping file '{FilePath}' of data source '{DataSourceName}' ({DataSourceId}) from now on because reading it failed for a reason which lies in the file. FailureCode={FailureCode}, MetadataHashPrefix={MetadataHashPrefix}.",
                    document.Key,
                    dataSource.Name,
                    dataSource.Id,
                    extractionFailure.Code,
                    ShortHash(state.Fingerprint));
                this.Publish(document.DisplayName);
                break;
            }

            default:
            {
                //
                // Everything which is not the provider's doing: a document which changed while it
                // was read, one which yielded no text, a vector store which refused to store. These
                // are about this one document, so they go into the list and not into a message
                // which would interrupt whatever the user is doing right now.
                //
                this.FailedDocuments++;
                var extractionCode = exception is FileExtractionException extractionFailure ? extractionFailure.Code : FileExtractionErrorCode.NONE;

                //
                // Deliberately not the message of the exception: that one is written for the log
                // file, in English, and repeats the path which the list shows anyway.
                //
                var failureMessage = extractionCode.ToIndexingUserMessage(document.DisplayName);
                this.LastError = failureMessage;
                this.failures.Add(new DataSourceEmbeddingFailure(document.Key, failureMessage, DateTimeOffset.UtcNow, EmbeddingProviderName: context.EmbeddingProvider.Name, ExtractionCode: extractionCode));
                context.Manifest.Files.Remove(document.Key);
                await context.ForgetPermanentFailureAsync(document.Key, token);
                await context.CleanupFailedDocumentAsync(document.Key, token);

                logger.LogWarning(exception, "Failed to embed file '{FilePath}' for data source '{DataSourceName}'.", document.Key, dataSource.Name);
                this.Publish(document.DisplayName);
                break;
            }
        }
    }

    /// <summary>
    /// Finishes the run: the collection is tidied up, the data source is marked as worked through,
    /// and the user interface learns how it went.
    /// </summary>
    /// <remarks>
    /// The hash is written last on purpose. It is what says that a run got through the whole data
    /// source, so a run which stops before this point leaves the data source marked as unfinished.
    /// </remarks>
    /// <param name="sourceHash">The hash of the data source as this run found it.</param>
    /// <param name="reason">Why the collection is optimized now, for the log.</param>
    /// <param name="token">The cancellation token.</param>
    public async Task CompleteRunAsync(string sourceHash, string reason, CancellationToken token)
    {
        context.Manifest.SourceHash = sourceHash;
        token.ThrowIfCancellationRequested();
        await context.OptimizeCollectionIfNeededAsync(reason, token);

        token.ThrowIfCancellationRequested();
        await context.IndexStore.UpdateDataSourceHashAsync(context.DataSource.Id, sourceHash, token);
        token.ThrowIfCancellationRequested();

        //
        // Documents which were skipped for good do not make a run unsuccessful: nothing is left to
        // try, and a data source made of nothing but scanned images would otherwise ask for
        // attention forever.
        //
        var hasFailures = this.FailedDocuments > 0;
        var lastError = hasFailures
            ? string.IsNullOrWhiteSpace(this.LastError)
                ? TB("Some files could not be indexed. The list below says which ones and why.")
                : this.LastError
            : string.Empty;

        context.PublishStatus(this.CreateStatus(hasFailures ? DataSourceEmbeddingState.FAILED : DataSourceEmbeddingState.COMPLETED, string.Empty, lastError, null, null));
    }

    private DataSourceEmbeddingStatus CreateStatus(DataSourceEmbeddingState state, string currentDocument, string lastError, int? currentBlock, int? currentPage) => new(
        context.DataSource.Id,
        context.DataSource.Name,
        context.DataSource.Type,
        state,
        totalDocuments,
        this.DoneDocuments,
        this.FailedDocuments,
        currentDocument,
        lastError,
        this.failures.ToList(),
        this.PermanentlySkippedDocuments,
        currentBlock,
        currentPage);
}