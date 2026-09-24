using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class DataSourceLocalRetrievalService(
    SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider,
    DataSourceEmbeddingService embeddingService, ILogger<DataSourceLocalRetrievalService> logger)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceLocalRetrievalService).Namespace, nameof(DataSourceLocalRetrievalService));

    //
    // Which gaps the user was already told about in this session. Retrieval runs for every single
    // message, so without this one broken embedding provider would put a warning on every prompt.
    //
    private readonly HashSet<string> reportedRetrievalGaps = new(StringComparer.Ordinal);
    private readonly Lock retrievalGapLock = new();

    private enum RetrievalChannel
    {
        VECTOR,
        BM25,
    }

    //
    // A hit keeps the complete shape both retrieval channels deliver, even where nothing reads a
    // value yet. Merging is deterministic on purpose for now, so Channel, Score and Rank have no
    // consumer until reranking arrives. Naming them still beats handing an unlabelled tuple of
    // strings and numbers through the service.
    //
    // ReSharper disable NotAccessedPositionalProperty.Local
    private sealed record LocalRetrievalHit(
        RetrievalChannel Channel,
        string ChunkId,
        string ParentFileId,
        string DataSourceId,
        string DataSourceType,
        string AbsolutePath,
        string FileName,
        string RelativePath,
        string FileType,
        int? PageNumber,
        int ChunkIndex,
        string Text,
        double Score,
        int Rank);
    // ReSharper restore NotAccessedPositionalProperty.Local

    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(DataSourceLocalFile dataSource, IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        this.RetrieveDataAsync(dataSource, lastUserPrompt, token);

    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(DataSourceLocalDirectory dataSource, IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        this.RetrieveDataAsync(dataSource, lastUserPrompt, token);

    public Task<RetrievalPage> RetrieveDataAsync(DataSourceLocalFile dataSource, string query, int page, ChatThread thread, CancellationToken token = default) =>
        this.RetrievePageAsync(dataSource, query, page, token);

    public Task<RetrievalPage> RetrieveDataAsync(DataSourceLocalDirectory dataSource, string query, int page, ChatThread thread, CancellationToken token = default) =>
        this.RetrievePageAsync(dataSource, query, page, token);

    private async Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IInternalDataSource dataSource, IContent lastUserPrompt, CancellationToken token)
    {
        // The first page is what this retrieval has always returned:
        var firstPage = await this.RetrievePageAsync(dataSource, GetQueryText(lastUserPrompt), 1, token);
        return firstPage.Contexts;
    }

    private async Task<RetrievalPage> RetrievePageAsync(IInternalDataSource dataSource, string query, int page, CancellationToken token)
    {
        var pageSize = (int)dataSource.MaxMatches;
        var window = RetrievalPaging.GetWindowSize(page, pageSize);
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogDebug("Skipping local retrieval for data source '{DataSourceName}' ({DataSourceId}) because there is no text to search for.", dataSource.Name, dataSource.Id);
            return RetrievalPage.EMPTY;
        }

        if (pageSize == 0)
            return RetrievalPage.EMPTY;

        //
        // A data source waiting for its index is kept out of the selection before the RAG process
        // starts. This catches whatever reaches retrieval another way, and turns an answer quietly
        // put together without the data into a sentence saying so.
        //
        // Asked here rather than inside one of the two channels below, because both of them read
        // what the rebuild is about to discard: with only the embedding signature changed, the old
        // chunks are still in place and the keyword search would happily answer from them while
        // the vector search finds nothing.
        //
        if (await embeddingService.IsAwaitingReindexAsync(dataSource, token))
        {
            logger.LogWarning("Skipping local retrieval for data source '{DataSourceName}' ({DataSourceId}) because its index has to be built anew.", dataSource.Name, dataSource.Id);
            await this.ReportRetrievalGapAsync(dataSource, "index-rebuilding", string.Format(TB("The data source '{0}' was left out of the answer: it is being indexed again and cannot be searched until that is finished."), dataSource.Name));
            return RetrievalPage.EMPTY;
        }

        var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);
        var vectorTask = this.SearchVectorAsync(dataSource, query, window, collectionName, token);
        var bm25Task = this.SearchBm25Async(dataSource, query, window, token);

        await Task.WhenAll(vectorTask, bm25Task);
        token.ThrowIfCancellationRequested();

        var (hits, hasMore) = RetrievalPaging.Merge(
            vectorTask.Result.Select((result, index) => FromVectorResult(result, index + 1)).ToList(),
            bm25Task.Result.Select((result, index) => FromBm25Result(result, index + 1)).ToList(),
            hit => hit.ChunkId,
            page,
            pageSize);

        logger.LogInformation(
            "Retrieved {MergedHits} local RAG hits on page {Page} for data source '{DataSourceName}' ({DataSourceId}). VectorCandidates={VectorHits}, BM25Candidates={BM25Hits}, RequestedPerChannel={RequestedPerChannel}, HasMore={HasMore}.",
            hits.Count,
            page,
            dataSource.Name,
            dataSource.Id,
            vectorTask.Result.Count,
            bm25Task.Result.Count,
            window,
            hasMore);

        var contexts = hits
            .Where(hit => !string.IsNullOrWhiteSpace(hit.Text))
            .Select(hit => ToRetrievalContext(hit, dataSource))
            .ToList();

        return new RetrievalPage(contexts, hasMore);
    }

    private async Task<IReadOnlyList<VectorSearchResult>> SearchVectorAsync(
        IInternalDataSource dataSource,
        string query,
        int maxMatches,
        string collectionName,
        CancellationToken token)
    {
        try
        {
            var vectorStore = await databaseClientProvider.GetVectorStoreAsync(token);
            if (!vectorStore.IsAvailable)
            {
                logger.LogWarning(
                    "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because vector store '{VectorStoreName}' is unavailable.",
                    dataSource.Name,
                    dataSource.Id,
                    vectorStore.Name);
                await this.ReportRetrievalGapAsync(dataSource, "no-vector-store", string.Format(TB("The data source '{0}' was left out of the answer: its local index is not available."), dataSource.Name));
                return [];
            }

            if (!DataSourceEmbeddingProviders.TryResolve(settingsManager, dataSource, out var embeddingProvider))
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the selected embedding provider is not available.", dataSource.Name, dataSource.Id);
                await this.ReportRetrievalGapAsync(dataSource, "no-embedding-provider", string.Format(TB("The data source '{0}' was left out of the answer: its embedding provider is not available. Please check it in the settings."), dataSource.Name));
                return [];
            }

            if (!await this.QueryFitsEmbeddingProviderAsync(dataSource, embeddingProvider, query, token))
                return [];

            var provider = embeddingProvider.CreateProvider();
            var vectors = await provider.EmbedTextAsync(embeddingProvider.Model, settingsManager, token, [query]);
            token.ThrowIfCancellationRequested();
            var vector = vectors.FirstOrDefault();
            if (vector is null || vector.Count == 0)
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because query embedding returned no vector.", dataSource.Name, dataSource.Id);
                await this.ReportRetrievalGapAsync(dataSource, "no-query-vector", string.Format(TB("The data source '{0}' was left out of the answer: its embedding provider '{1}' did not return a vector for your message."), dataSource.Name, embeddingProvider.Name));
                return [];
            }

            var results = this.LimitSearchResults(
                dataSource,
                "vector",
                await vectorStore.SearchEmbeddingAsync(collectionName, vector, maxMatches, token),
                maxMatches);
            this.LogVectorResults(dataSource, results);
            return results;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (ProviderRequestException exception)
        {
            //
            // The embedding provider named the cause and what to do about it. That sentence is
            // worth far more to the user than the fact that a search came back empty:
            //
            logger.LogWarning(
                exception,
                "Vector retrieval failed for data source '{DataSourceName}' ({DataSourceId}) because the embedding provider failed. FailureReason={FailureReason}, StatusCode={StatusCode}.",
                dataSource.Name, dataSource.Id, exception.FailureReason, exception.StatusCode);
            await this.ReportRetrievalGapAsync(dataSource, $"provider-{exception.FailureReason}", string.Format(TB("The data source '{0}' was left out of the answer. {1}"), dataSource.Name, exception.UserMessage));
            return [];
        }
        catch (VectorStoreUnreadableException exception)
        {
            //
            // Its own gap key, because this is not a search which went wrong but an index which has
            // to be built anew. Saying that once per session is what turns a silently shortened
            // answer into one the user can do something about.
            //
            logger.LogWarning(exception, "Vector retrieval failed for data source '{DataSourceName}' ({DataSourceId}) because its vector store cannot be read.", dataSource.Name, dataSource.Id);
            await this.ReportRetrievalGapAsync(dataSource, "vector-store-unreadable", string.Format(TB("The data source '{0}' was left out of the answer: its index cannot be read anymore. You can repair it in your data source settings."), dataSource.Name));
            return [];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Vector retrieval failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            await this.ReportRetrievalGapAsync(dataSource, "vector-search-failed", string.Format(TB("The data source '{0}' was left out of the answer because searching it failed."), dataSource.Name));
            return [];
        }
    }

    /// <summary>
    /// Tells the user once that a data source cannot take part in answering.
    /// </summary>
    /// <remarks>
    /// A failed search is not an error of the chat: the model still answers, only without what
    /// this data source knows. Saying so once is what keeps somebody from trusting an answer
    /// which was put together without half of its sources. Saying it with every prompt would be
    /// worse than saying nothing, which is why every gap is reported once per session.
    /// </remarks>
    /// <param name="dataSource">The data source which could not be searched.</param>
    /// <param name="gapKey">What kind of gap this is, so a different problem is reported again.</param>
    /// <param name="userMessage">What to tell the user.</param>
    private async Task ReportRetrievalGapAsync(IInternalDataSource dataSource, string gapKey, string userMessage)
    {
        lock (this.retrievalGapLock)
        {
            if (!this.reportedRetrievalGaps.Add($"{dataSource.Id}::{gapKey}"))
                return;
        }

        await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.SearchOff, userMessage));
    }

    private async Task<bool> QueryFitsEmbeddingProviderAsync(
        IInternalDataSource dataSource,
        EmbeddingProvider embeddingProvider,
        string query,
        CancellationToken token)
    {
        var providerTokenLimit = Math.Max(1, embeddingProvider.EffectiveTokenLimit);
        if (query.Length > RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH)
        {
            logger.LogWarning(
                "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the latest prompt has {CharacterCount} characters and exceeds the safe tokenizer request length of {MaxCharacterCount}. ProviderTokenLimit={ProviderTokenLimit}.",
                dataSource.Name,
                dataSource.Id,
                query.Length,
                RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH,
                providerTokenLimit);
            await this.ReportRetrievalGapAsync(dataSource, "query-too-long", string.Format(TB("The data source '{0}' was left out of the answer because your message is too long to search with."), dataSource.Name));
            return false;
        }

        var tokenCountResponse = await rustService.GetTokenCount(embeddingProvider, query, token);
        if (tokenCountResponse is not { Success: true })
        {
            logger.LogWarning(
                "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the token count for embedding provider '{EmbeddingProviderName}' could not be determined. Reason='{Reason}'.",
                dataSource.Name,
                dataSource.Id,
                embeddingProvider.Name,
                tokenCountResponse?.Message ?? "No response was returned by the tokenizer service.");
            await this.ReportRetrievalGapAsync(dataSource, "no-token-count", string.Format(TB("The data source '{0}' was left out of the answer: the tokenizer of its embedding provider '{1}' is not available."), dataSource.Name, embeddingProvider.Name));
            return false;
        }

        var queryTokenCount = tokenCountResponse.Value.TokenCount;
        if (queryTokenCount > providerTokenLimit)
        {
            logger.LogWarning(
                "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the latest prompt has {QueryTokenCount} tokens, exceeding embedding provider '{EmbeddingProviderName}' limit of {ProviderTokenLimit} tokens.",
                dataSource.Name,
                dataSource.Id,
                queryTokenCount,
                embeddingProvider.Name,
                providerTokenLimit);
            await this.ReportRetrievalGapAsync(dataSource, "query-over-token-limit", string.Format(TB("The data source '{0}' was left out of the answer because your message is longer than its embedding provider '{1}' accepts."), dataSource.Name, embeddingProvider.Name));
            return false;
        }

        return true;
    }

    private async Task<IReadOnlyList<IndexStoreSearchResult>> SearchBm25Async(IInternalDataSource dataSource, string query, int maxMatches, CancellationToken token)
    {
        try
        {
            var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
            if (!indexStore.IsAvailable)
            {
                logger.LogWarning(
                    "Skipping BM25 retrieval for data source '{DataSourceName}' ({DataSourceId}) because local RAG index '{DatabaseName}' is unavailable.",
                    dataSource.Name,
                    dataSource.Id,
                    indexStore.Name);
                return [];
            }

            var results = this.LimitSearchResults(
                dataSource,
                "BM25",
                await indexStore.SearchChunksAsync(dataSource.Id, query, maxMatches, token),
                maxMatches);
            this.LogBm25Results(dataSource, results);
            return results;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "BM25 retrieval failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            return [];
        }
    }

    private IReadOnlyList<T> LimitSearchResults<T>(IInternalDataSource dataSource, string searchName, IReadOnlyList<T> results, int maxMatches)
    {
        if (results.Count <= maxMatches)
            return results;

        logger.LogWarning(
            "Local RAG {SearchName} search returned {ReturnedHits} chunks for data source '{DataSourceName}' ({DataSourceId}), which exceeds the requested maximum {MaxMatches}. Truncating to it.",
            searchName,
            results.Count,
            dataSource.Name,
            dataSource.Id,
            maxMatches);

        return results.Take(maxMatches).ToList();
    }

    private static LocalRetrievalHit FromVectorResult(VectorSearchResult result, int rank) =>
        new(
            RetrievalChannel.VECTOR,
            result.ChunkId,
            result.ParentFileId,
            result.DataSourceId,
            result.DataSourceType,
            FirstNonEmpty(result.AbsolutePath, result.FilePath),
            result.FileName,
            result.RelativePath,
            result.FileType,
            result.PageNumber,
            result.ChunkIndex,
            result.Text,
            result.Score,
            rank);

    private static LocalRetrievalHit FromBm25Result(IndexStoreSearchResult result, int rank) =>
        new(
            RetrievalChannel.BM25,
            result.ChunkId,
            result.ParentFileId,
            result.DataSourceId,
            result.DataSourceType,
            result.AbsolutePath,
            result.FileName,
            result.RelativePath,
            result.FileType,
            result.PageNumber,
            result.ChunkIndex,
            result.ChunkText,
            result.Score,
            rank);

    private static RetrievalTextContext ToRetrievalContext(LocalRetrievalHit hit, IInternalDataSource dataSource)
    {
        var sourceName = FirstNonEmpty(hit.FileName, dataSource.Name);
        var path = FirstNonEmpty(hit.AbsolutePath, hit.RelativePath);
        var referenceLink = string.IsNullOrWhiteSpace(path) ? string.Empty : BuildReferenceLink(path, hit);

        return new RetrievalTextContext
        {
            DataSourceName = sourceName,
            Category = RetrievalContentCategory.TEXT,
            Type = GetRetrievalContentType(hit.FileType),
            Path = path,
            Links = [],
            MatchedText = hit.Text,
            SurroundingContent = [],
            ReferenceTitle = BuildReferenceTitle(hit, dataSource),
            ReferenceLink = referenceLink,
            PageNumber = hit.PageNumber is > 0 ? hit.PageNumber : null,
        };
    }

    private static string BuildReferenceTitle(LocalRetrievalHit hit, IInternalDataSource dataSource)
    {
        var sourceName = FirstNonEmpty(hit.FileName, dataSource.Name);
        return BuildLocatedReferenceTitle(sourceName, hit.ChunkIndex, hit.PageNumber);
    }

    private static string BuildLocatedReferenceTitle(string sourceName, int chunkIndex, int? pageNumber)
    {
        var location = pageNumber is > 0
            ? string.Format(TB("Page {0}"), pageNumber)
            : string.Format(TB("Chunk {0}"), chunkIndex + 1);

        return $"{sourceName} ({location})";
    }

    /// <remarks>
    /// A known page is written as the fragment `#page=N`, which is what the PDF open parameters
    /// call for: a program which understands them opens the document where the passage is. Without
    /// a page there is nothing to send a program to, and the chunk stays in the link so the
    /// reference still points at something.
    /// </remarks>
    private static string BuildReferenceLink(string path, LocalRetrievalHit hit)
    {
        var link = NormalizeLocalReferencePath(path);
        var separator = link.Contains('#', StringComparison.Ordinal) ? "&" : "#";
        return hit.PageNumber is > 0
            ? $"{link}{separator}page={hit.PageNumber}"
            : $"{link}{separator}chunk={hit.ChunkIndex}";
    }

    private static string NormalizeLocalReferencePath(string path)
    {
        try
        {
            return Path.IsPathRooted(path)
                ? new Uri(Path.GetFullPath(path)).AbsoluteUri
                : path;
        }
        catch
        {
            return path;
        }
    }

    private static RetrievalContentType GetRetrievalContentType(string fileType)
    {
        if (FileTypes.IsAllowedExtension(fileType, FileTypes.TABULAR, FileTypes.SPREADSHEET))
            return RetrievalContentType.TEXT_SPREADSHEET;

        if (FileTypes.IsAllowedExtension(fileType, FileTypes.POWER_POINT))
            return RetrievalContentType.TEXT_PRESENTATION;

        return FileTypes.IsAllowedExtension(fileType, FileTypes.HTML)
            ? RetrievalContentType.TEXT_WEBSITE
            : RetrievalContentType.TEXT_DOCUMENT;
    }

    private static string GetQueryText(IContent lastUserPrompt) => lastUserPrompt switch
    {
        ContentText text => text.Text,
        _ => string.Empty
    };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private void LogVectorResults(IInternalDataSource dataSource, IReadOnlyList<VectorSearchResult> results)
    {
        if (results.Count == 0)
        {
            logger.LogInformation("Local RAG vector search found no chunks for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            return;
        }

        foreach (var result in results.Select((result, index) => (Result: result, Rank: index + 1)))
        {
            logger.LogInformation(
                "Local RAG vector search found chunk for data source '{DataSourceName}' ({DataSourceId}). Rank={Rank}, Score={Score}, ChunkId='{ChunkId}', ParentFileId='{ParentFileId}', File='{FileName}', Path='{Path}', Title='{Title}'.",
                dataSource.Name,
                dataSource.Id,
                result.Rank,
                result.Result.Score,
                result.Result.ChunkId,
                result.Result.ParentFileId,
                result.Result.FileName,
                FirstNonEmpty(result.Result.AbsolutePath, result.Result.FilePath),
                BuildLocatedReferenceTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), result.Result.ChunkIndex, result.Result.PageNumber));
        }
    }

    private void LogBm25Results(IInternalDataSource dataSource, IReadOnlyList<IndexStoreSearchResult> results)
    {
        if (results.Count == 0)
        {
            logger.LogInformation("Local RAG BM25 search found no chunks for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            return;
        }

        foreach (var result in results.Select((result, index) => (Result: result, Rank: index + 1)))
        {
            logger.LogInformation(
                "Local RAG BM25 search found chunk for data source '{DataSourceName}' ({DataSourceId}). Rank={Rank}, Score={Score}, ChunkId='{ChunkId}', ParentFileId='{ParentFileId}', File='{FileName}', Path='{Path}', Title='{Title}'.",
                dataSource.Name,
                dataSource.Id,
                result.Rank,
                result.Result.Score,
                result.Result.ChunkId,
                result.Result.ParentFileId,
                result.Result.FileName,
                result.Result.AbsolutePath,
                BuildLocatedReferenceTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), result.Result.ChunkIndex, result.Result.PageNumber));
        }
    }
}
