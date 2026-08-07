using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.EmbeddingState;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.RAG;

namespace AIStudio.Tools.Services;

public sealed class DataSourceLocalRetrievalService(
    SettingsManager settingsManager,
    DatabaseClientProvider databaseClientProvider,
    ILogger<DataSourceLocalRetrievalService> logger)
{
    private enum RetrievalChannel
    {
        VECTOR,
        BM25,
    }

    private sealed record LocalRetrievalHit(
        RetrievalChannel Channel,
        string ChunkId,
        string ParentFileId,
        string DataSourceId,
        string DataSourceName,
        string DataSourceType,
        string AbsolutePath,
        string FileName,
        string RelativePath,
        string FileType,
        int? PageNumber,
        int ChunkIndex,
        string Text,
        double Score,
        int Rank,
        string ComplianceLevel,
        int ComplianceLevelRank);

    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(DataSourceLocalFile dataSource, IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        this.RetrieveDataAsync((IInternalDataSource)dataSource, lastUserPrompt, token);

    public Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(DataSourceLocalDirectory dataSource, IContent lastUserPrompt, ChatThread thread, CancellationToken token = default) =>
        this.RetrieveDataAsync((IInternalDataSource)dataSource, lastUserPrompt, token);

    private async Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IInternalDataSource dataSource, IContent lastUserPrompt, CancellationToken token)
    {
        var query = GetQueryText(lastUserPrompt);
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogDebug("Skipping local retrieval for data source '{DataSourceName}' ({DataSourceId}) because the latest prompt does not contain text.", dataSource.Name, dataSource.Id);
            return [];
        }

        var maxMatches = (int)dataSource.MaxMatches;
        if (maxMatches == 0)
            return [];

        var collectionName = DataSourceEmbeddingNames.GetCollectionName(dataSource.Name, dataSource.Id);
        var vectorTask = this.SearchVectorAsync(dataSource, query, maxMatches, collectionName, token);
        var bm25Task = this.SearchBm25Async(dataSource, query, maxMatches, token);

        await Task.WhenAll(vectorTask, bm25Task);
        token.ThrowIfCancellationRequested();

        var hits = MergeResults(vectorTask.Result, bm25Task.Result, maxMatches);
        logger.LogInformation(
            "Retrieved {MergedHits} local RAG hits for data source '{DataSourceName}' ({DataSourceId}). VectorCandidates={VectorHits}, BM25Candidates={BM25Hits}, RequestedPerChannel={RequestedPerChannel}.",
            hits.Count,
            dataSource.Name,
            dataSource.Id,
            vectorTask.Result.Count,
            bm25Task.Result.Count,
            maxMatches);

        return hits
            .Where(hit => !string.IsNullOrWhiteSpace(hit.Text))
            .Select(ToRetrievalContext)
            .ToList();
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
                return [];
            }

            if (!DataSourceEmbeddingProviders.TryResolve(settingsManager, dataSource, out var embeddingProvider))
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because the selected embedding provider is not available.", dataSource.Name, dataSource.Id);
                return [];
            }

            var provider = embeddingProvider.CreateProvider();
            var vectors = await provider.EmbedTextAsync(embeddingProvider.Model, settingsManager, token, [query]);
            token.ThrowIfCancellationRequested();
            var vector = vectors.FirstOrDefault();
            if (vector is null || vector.Count == 0)
            {
                logger.LogWarning("Skipping vector retrieval for data source '{DataSourceName}' ({DataSourceId}) because query embedding returned no vector.", dataSource.Name, dataSource.Id);
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
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Vector retrieval failed for data source '{DataSourceName}' ({DataSourceId}).", dataSource.Name, dataSource.Id);
            return [];
        }
    }

    private async Task<IReadOnlyList<EmbeddingStateSearchResult>> SearchBm25Async(IInternalDataSource dataSource, string query, int maxMatches, CancellationToken token)
    {
        try
        {
            var embeddingState = await databaseClientProvider.GetEmbeddingStateAsync(token);
            if (!embeddingState.IsAvailable)
            {
                logger.LogWarning(
                    "Skipping BM25 retrieval for data source '{DataSourceName}' ({DataSourceId}) because local RAG index '{DatabaseName}' is unavailable.",
                    dataSource.Name,
                    dataSource.Id,
                    embeddingState.Name);
                return [];
            }

            var results = this.LimitSearchResults(
                dataSource,
                "BM25",
                await embeddingState.SearchChunksAsync(dataSource.Id, query, maxMatches, token),
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
            "Local RAG {SearchName} search returned {ReturnedHits} chunks for data source '{DataSourceName}' ({DataSourceId}), which exceeds the configured maximum {MaxMatches}. Truncating to the datasource limit.",
            searchName,
            results.Count,
            dataSource.Name,
            dataSource.Id,
            maxMatches);

        return results.Take(maxMatches).ToList();
    }

    private static IReadOnlyList<LocalRetrievalHit> MergeResults(
        IReadOnlyList<VectorSearchResult> vectorResults,
        IReadOnlyList<EmbeddingStateSearchResult> bm25Results,
        int maxMatches)
    {
        // Future reranking should replace this deterministic channel merge.
        var merged = new List<LocalRetrievalHit>(maxMatches * 2);
        var seenChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AppendHits(
            merged,
            seenChunkIds,
            vectorResults
                .Select((result, index) => FromVectorResult(result, index + 1)),
            maxMatches);

        AppendHits(
            merged,
            seenChunkIds,
            bm25Results
                .Select((result, index) => FromBm25Result(result, index + 1)),
            maxMatches);

        return merged;
    }

    private static void AppendHits(List<LocalRetrievalHit> merged, HashSet<string> seenChunkIds, IEnumerable<LocalRetrievalHit> hits, int maxNewHits)
    {
        var added = 0;
        foreach (var hit in hits)
        {
            if (!string.IsNullOrWhiteSpace(hit.ChunkId) && !seenChunkIds.Add(hit.ChunkId))
                continue;

            merged.Add(hit);
            added++;
            if (added >= maxNewHits)
                return;
        }
    }

    private static LocalRetrievalHit FromVectorResult(VectorSearchResult result, int rank) =>
        new(
            RetrievalChannel.VECTOR,
            result.ChunkId,
            result.ParentFileId,
            result.DataSourceId,
            result.DataSourceName,
            result.DataSourceType,
            FirstNonEmpty(result.AbsolutePath, result.FilePath),
            result.FileName,
            result.RelativePath,
            result.FileType,
            result.PageNumber,
            result.ChunkIndex,
            result.Text,
            result.Score,
            rank,
            result.ComplianceLevel,
            result.ComplianceLevelRank);

    private static LocalRetrievalHit FromBm25Result(EmbeddingStateSearchResult result, int rank) =>
        new(
            RetrievalChannel.BM25,
            result.ChunkId,
            result.ParentFileId,
            result.DataSourceId,
            result.DataSourceName,
            result.DataSourceType,
            result.AbsolutePath,
            result.FileName,
            result.RelativePath,
            result.FileType,
            result.PageNumber,
            result.ChunkIndex,
            result.ChunkText,
            result.Score,
            rank,
            result.ComplianceLevel,
            result.ComplianceLevelRank);

    private static RetrievalTextContext ToRetrievalContext(LocalRetrievalHit hit)
    {
        var sourceName = FirstNonEmpty(hit.FileName, hit.DataSourceName);
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
            ReferenceTitle = BuildReferenceTitle(hit),
            ReferenceLink = referenceLink,
        };
    }

    private static string BuildReferenceTitle(LocalRetrievalHit hit)
    {
        var sourceName = FirstNonEmpty(hit.FileName, hit.DataSourceName);
        return BuildChunkTitle(sourceName, hit.ChunkIndex, hit.PageNumber);
    }

    private static string BuildChunkTitle(string sourceName, int chunkIndex, int? pageNumber)
    {
        var page = pageNumber is > 0 ? $", page {pageNumber}" : string.Empty;
        return $"{sourceName} (chunk {chunkIndex + 1}{page})";
    }

    private static string BuildReferenceLink(string path, LocalRetrievalHit hit)
    {
        var link = NormalizeLocalReferencePath(path);
        var separator = link.Contains('#', StringComparison.Ordinal) ? "&" : "#";
        return $"{link}{separator}chunk={hit.ChunkIndex}";
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

    private static RetrievalContentType GetRetrievalContentType(string fileType) => fileType.TrimStart('.').ToLowerInvariant() switch
    {
        "csv" or "tsv" or "ods" or "xls" or "xlsx" or "xlsm" or "xlsb" => RetrievalContentType.TEXT_SPREADSHEET,
        "odp" or "ppt" or "pptx" => RetrievalContentType.TEXT_PRESENTATION,
        "htm" or "html" => RetrievalContentType.TEXT_WEBSITE,
        _ => RetrievalContentType.TEXT_DOCUMENT
    };

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
                BuildChunkTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), result.Result.ChunkIndex, result.Result.PageNumber));
        }
    }

    private void LogBm25Results(IInternalDataSource dataSource, IReadOnlyList<EmbeddingStateSearchResult> results)
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
                BuildChunkTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), result.Result.ChunkIndex, result.Result.PageNumber));
        }
    }
}
