using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.RAG;

namespace AIStudio.Tools.Services;

/// <summary>
/// Searches the local index of one data source: its vectors by meaning, its keyword index by words.
/// </summary>
/// <remarks>
/// Shared by everything which searches what AI Studio indexed itself, the local files and folders
/// as well as the mailboxes. Each of them decides what to search for and what to make of the
/// matches. What they share is how a search which cannot cover the whole data source turns into a
/// gap the user hears about, instead of into an answer quietly put together without it.<br/><br/>
/// Nothing here logs what a match holds. The name of a file may go into the log, the subject of a
/// mail must not, so whoever searches logs the matches itself.
/// </remarks>
public sealed class LocalIndexSearchService(SettingsManager settingsManager, RustService rustService, DatabaseClientProvider databaseClientProvider, DataSourceEmbeddingService embeddingService, ILogger<LocalIndexSearchService> logger)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LocalIndexSearchService).Namespace, nameof(LocalIndexSearchService));

    //
    // Which gaps the user was already told about in this session. Retrieval runs for every single
    // message, so without this one broken embedding provider would put a warning on every prompt.
    //
    private readonly HashSet<string> reportedRetrievalGaps = new(StringComparer.Ordinal);
    private readonly Lock retrievalGapLock = new();

    /// <summary>
    /// Whether the index of a data source has to be built anew before it can be searched.
    /// </summary>
    /// <remarks>
    /// Whoever searches asks this before either channel does, since both of them read what the
    /// rebuild is about to discard.
    /// </remarks>
    /// <param name="dataSource">The data source to search.</param>
    /// <param name="run">The retrieval, which records the gap when the data source cannot be searched.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>True when the data source cannot be searched until its index is rebuilt.</returns>
    public async Task<bool> IsAwaitingReindexAsync(IIndexedDataSource dataSource, RetrievalRun run, CancellationToken token)
    {
        if (!await embeddingService.IsAwaitingReindexAsync(dataSource, token))
            return false;

        logger.LogWarning("Skipping local retrieval for data source '{DataSourceName}' ({DataSourceId}) because its index has to be built anew.", dataSource.Name, dataSource.Id);
        await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.NOT_SEARCHED, "index-rebuilding", string.Format(TB("The data source '{0}' was left out of the answer: it is being indexed again and cannot be searched until that is finished."), dataSource.Name));
        return true;
    }

    /// <summary>
    /// Searches the vectors of a data source for the chunks closest in meaning to the query.
    /// </summary>
    /// <param name="dataSource">The data source to search.</param>
    /// <param name="query">What to search for.</param>
    /// <param name="maxMatches">How many chunks to return at most.</param>
    /// <param name="filter">The chunks the search may return, or null to search all of them.</param>
    /// <param name="run">The retrieval, which records what kept the search from covering the data source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunks, best first. Empty when nothing matched, or when the search could not run, which the run records then.</returns>
    public async Task<IReadOnlyList<VectorSearchResult>> SearchVectorsAsync(IIndexedDataSource dataSource, string query, int maxMatches, VectorSearchFilter? filter, RetrievalRun run, CancellationToken token)
    {
        // Nothing can match, so there is no need to send the query to the embedding provider:
        if (filter is { MatchesNothing: true })
            return [];

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
                await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "no-vector-store", string.Format(TB("The data source '{0}' was left out of the answer: its local index is not available."), dataSource.Name));
                return [];
            }

            if (!DataSourceEmbeddingProviders.TryResolve(settingsManager, dataSource, out var embeddingProvider))
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the selected embedding provider is not available.", dataSource.Name, dataSource.Id);
                await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "no-embedding-provider", string.Format(TB("The data source '{0}' was left out of the answer: its embedding provider is not available. Please check it in the settings."), dataSource.Name));
                return [];
            }

            if (!await this.QueryFitsEmbeddingProviderAsync(dataSource, embeddingProvider, query, run, token))
                return [];

            var provider = embeddingProvider.CreateProvider();
            var vectors = await provider.EmbedTextAsync(embeddingProvider.Model, settingsManager, token, [query]);
            token.ThrowIfCancellationRequested();
            var vector = vectors.FirstOrDefault();
            if (vector is null || vector.Count == 0)
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because query embedding returned no vector.", dataSource.Name, dataSource.Id);
                await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "no-query-vector", string.Format(TB("The data source '{0}' was left out of the answer: its embedding provider '{1}' did not return a vector to search with."), dataSource.Name, embeddingProvider.Name));
                return [];
            }

            var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Id);
            var results = filter is null
                ? await vectorStore.SearchEmbeddingAsync(collectionName, vector, maxMatches, token)
                : await vectorStore.SearchEmbeddingAsync(collectionName, vector, maxMatches, filter, token);

            return this.LimitSearchResults(dataSource, "vector", results, maxMatches);
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
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, $"provider-{exception.FailureReason}", string.Format(TB("The data source '{0}' was left out of the answer. {1}"), dataSource.Name, exception.UserMessage));
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
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "vector-store-unreadable", string.Format(TB("The data source '{0}' was left out of the answer: its index cannot be read anymore. You can repair it in your data source settings."), dataSource.Name));
            return [];
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Vector retrieval failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "vector-search-failed", string.Format(TB("The data source '{0}' was left out of the answer because searching it failed."), dataSource.Name));
            return [];
        }
    }

    /// <summary>
    /// Searches the keyword index of a data source with BM25.
    /// </summary>
    /// <param name="dataSource">The data source to search.</param>
    /// <param name="maxMatches">How many chunks to return at most.</param>
    /// <param name="search">The search to run against the keyword index, asking for at most maxMatches chunks of this data source.</param>
    /// <param name="run">The retrieval, which records what kept the search from covering the data source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunks, best first. Empty when nothing matched, or when the search could not run, which the run records then.</returns>
    public async Task<IReadOnlyList<IndexStoreSearchResult>> SearchKeywordsAsync(IIndexedDataSource dataSource, int maxMatches, Func<IndexStoreClient, Task<IReadOnlyList<IndexStoreSearchResult>>> search, RetrievalRun run, CancellationToken token)
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
                run.Add(RetrievalGap.PARTLY_SEARCHED);
                return [];
            }

            return this.LimitSearchResults(dataSource, "BM25", await search(indexStore), maxMatches);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "BM25 retrieval failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            run.Add(RetrievalGap.PARTLY_SEARCHED);
            return [];
        }
    }

    /// <summary>
    /// Records that a data source cannot fully take part in answering, and tells the user once.
    /// </summary>
    /// <remarks>
    /// A failed search is not an error of the chat: the model still answers, only without what
    /// this data source knows. Saying so once is what keeps somebody from trusting an answer
    /// which was put together without half of its sources. Saying it with every prompt would be
    /// worse than saying nothing, which is why every gap is reported once per session.
    ///
    /// The retrieval records every gap regardless, cf. RetrievalPage.Gaps: whoever asked for the
    /// page has to know each time, not once per session.
    /// </remarks>
    /// <param name="dataSource">The data source which could not be searched.</param>
    /// <param name="run">The retrieval this gap belongs to.</param>
    /// <param name="gap">What the gap means for the search.</param>
    /// <param name="gapKey">What kind of gap this is, so a different problem is reported again.</param>
    /// <param name="userMessage">What to tell the user.</param>
    private async Task ReportRetrievalGapAsync(IIndexedDataSource dataSource, RetrievalRun run, RetrievalGap gap, string gapKey, string userMessage)
    {
        run.Add(gap);
        if (!IsForTheUser(gap, run.QueryWrittenByUser))
            return;

        lock (this.retrievalGapLock)
        {
            if (!this.reportedRetrievalGaps.Add($"{dataSource.Id}::{gapKey}"))
                return;
        }

        await MessageBus.INSTANCE.SendWarning(new(Icons.Material.Filled.SearchOff, userMessage));
    }

    /// <summary>
    /// Whether the user has to hear about a gap.
    /// </summary>
    /// <remarks>
    /// Problems of the data source are for the user, since only the user can fix them. Problems of
    /// the query are for whoever wrote it. When the model worked the query out, telling the user
    /// their message was too long would be wrong, and the model learns about it from the page and
    /// can search with a shorter one.
    /// </remarks>
    /// <param name="gap">What the gap means for the search.</param>
    /// <param name="queryWrittenByUser">Whether the query is the user's own message.</param>
    /// <returns>True when the user has to be told.</returns>
    internal static bool IsForTheUser(RetrievalGap gap, bool queryWrittenByUser) => gap is not RetrievalGap.QUERY_NOT_SEARCHABLE || queryWrittenByUser;

    private async Task<bool> QueryFitsEmbeddingProviderAsync(
        IIndexedDataSource dataSource,
        EmbeddingProvider embeddingProvider,
        string query,
        RetrievalRun run,
        CancellationToken token)
    {
        var providerTokenLimit = Math.Max(1, embeddingProvider.EffectiveTokenLimit);
        if (query.Length > RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH)
        {
            logger.LogWarning(
                "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the query has {CharacterCount} characters and exceeds the safe tokenizer request length of {MaxCharacterCount}. ProviderTokenLimit={ProviderTokenLimit}.",
                dataSource.Name,
                dataSource.Id,
                query.Length,
                RustService.MAX_TOKEN_COUNT_REQUEST_TEXT_LENGTH,
                providerTokenLimit);
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.QUERY_NOT_SEARCHABLE, "query-too-long", string.Format(TB("The data source '{0}' was left out of the answer because your message is too long to search with."), dataSource.Name));
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
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.PARTLY_SEARCHED, "no-token-count", string.Format(TB("The data source '{0}' was left out of the answer: the tokenizer of its embedding provider '{1}' is not available."), dataSource.Name, embeddingProvider.Name));
            return false;
        }

        var queryTokenCount = tokenCountResponse.Value.TokenCount;
        if (queryTokenCount > providerTokenLimit)
        {
            logger.LogWarning(
                "Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the query has {QueryTokenCount} tokens, exceeding embedding provider '{EmbeddingProviderName}' limit of {ProviderTokenLimit} tokens.",
                dataSource.Name,
                dataSource.Id,
                queryTokenCount,
                embeddingProvider.Name,
                providerTokenLimit);
            await this.ReportRetrievalGapAsync(dataSource, run, RetrievalGap.QUERY_NOT_SEARCHABLE, "query-over-token-limit", string.Format(TB("The data source '{0}' was left out of the answer because your message is longer than its embedding provider '{1}' accepts."), dataSource.Name, embeddingProvider.Name));
            return false;
        }

        return true;
    }

    private IReadOnlyList<T> LimitSearchResults<T>(IIndexedDataSource dataSource, string searchName, IReadOnlyList<T> results, int maxMatches)
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
}