using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class DataSourceLocalRetrievalService(LocalIndexSearchService indexSearch, ILogger<DataSourceLocalRetrievalService> logger)
{
    private enum RetrievalChannel
    {
        VECTOR,
        BM25,
    }

    /// <summary>
    /// The rows or lines of a passage which names none.
    /// </summary>
    private static readonly (int? Start, int? End) NO_RANGE = (null, null);

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
        ChunkLocation Location,
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
        this.RetrievePageAsync(dataSource, query, page, new RetrievalRun(queryWrittenByUser: false), token);

    public Task<RetrievalPage> RetrieveDataAsync(DataSourceLocalDirectory dataSource, string query, int page, ChatThread thread, CancellationToken token = default) =>
        this.RetrievePageAsync(dataSource, query, page, new RetrievalRun(queryWrittenByUser: false), token);

    private async Task<IReadOnlyList<IRetrievalContext>> RetrieveDataAsync(IInternalDataSource dataSource, IContent lastUserPrompt, CancellationToken token)
    {
        // The first page is what this retrieval has always returned:
        var firstPage = await this.RetrievePageAsync(dataSource, GetQueryText(lastUserPrompt), 1, new RetrievalRun(queryWrittenByUser: true), token);
        return firstPage.Contexts;
    }

    private async Task<RetrievalPage> RetrievePageAsync(IInternalDataSource dataSource, string query, int page, RetrievalRun run, CancellationToken token)
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
        if (await indexSearch.IsAwaitingReindexAsync(dataSource, run, token))
            return RetrievalPage.EMPTY with { Gaps = run.GetGaps() };

        var vectorTask = indexSearch.SearchVectorsAsync(dataSource, query, window, filter: null, run, token);
        var bm25Task = indexSearch.SearchKeywordsAsync(dataSource, window, indexStore => indexStore.SearchChunksAsync(dataSource.Id, query, window, token), run, token);

        await Task.WhenAll(vectorTask, bm25Task);
        token.ThrowIfCancellationRequested();

        this.LogVectorResults(dataSource, vectorTask.Result);
        this.LogBm25Results(dataSource, bm25Task.Result);

        var (hits, hasMore) = RetrievalPaging.Merge(
            vectorTask.Result.Select((result, index) => FromVectorResult(result, index + 1)).ToList(),
            bm25Task.Result.Select((result, index) => FromBm25Result(result, index + 1)).ToList(),
            hit => hit.ChunkId,
            page,
            pageSize);

        var gaps = run.GetGaps();
        logger.LogInformation(
            "Retrieved {MergedHits} local RAG hits on page {Page} for data source '{DataSourceName}' ({DataSourceId}). VectorCandidates={VectorHits}, BM25Candidates={BM25Hits}, RequestedPerChannel={RequestedPerChannel}, HasMore={HasMore}, Gaps=[{Gaps}].",
            hits.Count,
            page,
            dataSource.Name,
            dataSource.Id,
            vectorTask.Result.Count,
            bm25Task.Result.Count,
            window,
            hasMore,
            string.Join(", ", gaps));

        var contexts = hits
            .Where(hit => !string.IsNullOrWhiteSpace(hit.Text))
            .Select(hit => ToRetrievalContext(hit, dataSource))
            .ToList();

        return new RetrievalPage(contexts, hasMore) { Gaps = gaps };
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
            result.Location,
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
            result.Location,
            result.ChunkIndex,
            result.ChunkText,
            result.Score,
            rank);

    private static RetrievalTextContext ToRetrievalContext(LocalRetrievalHit hit, IInternalDataSource dataSource)
    {
        var sourceName = FirstNonEmpty(hit.FileName, dataSource.Name);
        var path = FirstNonEmpty(hit.AbsolutePath, hit.RelativePath);
        var referenceLink = string.IsNullOrWhiteSpace(path) ? string.Empty : BuildReferenceLink(path, hit);
        var type = GetRetrievalContentType(hit.FileType);
        var located = LocatePassage(hit.Location, type, hit.ChunkIndex + 1);

        return new RetrievalTextContext
        {
            DataSourceName = sourceName,
            Category = RetrievalContentCategory.TEXT,
            Type = type,
            Path = path,
            Links = [],
            MatchedText = hit.Text,
            SurroundingContent = [],
            ReferenceTitle = BuildReferenceTitle(sourceName, located),
            ReferenceLink = referenceLink,
            PageNumber = located.PageNumber,
            EndPageNumber = located.EndPageNumber,
            SheetName = located.SheetName,
            StartRow = located.StartRow,
            EndRow = located.EndRow,
            StartLine = located.StartLine,
            EndLine = located.EndLine,
            ChunkNumber = located.ChunkNumber,
        };
    }

    /// <summary>
    /// Where a hit is in its document, as its passage names it.
    /// </summary>
    /// <remarks>
    /// An end only names a range when the passage goes on beyond where it starts. A chunk indexed
    /// before an end, a row or a line was kept has none, and names what it has. The lines of a
    /// table, such as a CSV file, are its rows. A transcript names no lines: it is a file the user
    /// never sees, so its lines would point nowhere.
    /// </remarks>
    /// <param name="location">Where the chunk of the hit is.</param>
    /// <param name="type">What kind of content the hit is.</param>
    /// <param name="chunkNumber">Which part of its document the hit is, counted from one.</param>
    /// <returns>A passage holding nothing but where the hit is.</returns>
    internal static SourcePassage LocatePassage(ChunkLocation location, RetrievalContentType type, int chunkNumber)
    {
        var pageNumber = location.PageNumber is > 0 ? location.PageNumber : null;
        var rows = RangeOf(location.StartRow, location.EndRow);
        var lines = type is RetrievalContentType.TEXT_TRANSCRIPT ? NO_RANGE : RangeOf(location.StartLine, location.EndLine);
        if (type is RetrievalContentType.TEXT_SPREADSHEET && rows.Start is null)
        {
            rows = lines;
            lines = NO_RANGE;
        }

        return new SourcePassage
        {
            Type = type,
            PageNumber = pageNumber,
            EndPageNumber = pageNumber is not null && location.EndPageNumber > pageNumber ? location.EndPageNumber : null,
            SheetName = rows.Start is null ? null : location.SheetName,
            StartRow = rows.Start,
            EndRow = rows.End,
            StartLine = lines.Start,
            EndLine = lines.End,
            ChunkNumber = chunkNumber,
        };
    }

    private static (int? Start, int? End) RangeOf(int? start, int? end) => start is > 0 ? (start, end > start ? end : null) : NO_RANGE;

    //
    // Worded like the location of every passage, so that the title reads the same before and after
    // the passages of an answer rename it, cf. SourcePassageExtensions.NamePassagesInTitles:
    //
    private static string BuildReferenceTitle(string sourceName, SourcePassage located) => $"{sourceName} ({located.GetLocationLabel()})";

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
        return hit.Location.PageNumber is > 0
            ? $"{link}{separator}page={hit.Location.PageNumber}"
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

    internal static RetrievalContentType GetRetrievalContentType(string fileType)
    {
        // An audio or a video file is indexed by its transcript, so that is what its passages are:
        if (FileTypes.IsAllowedExtension(fileType, FileTypes.AUDIO, FileTypes.VIDEO))
            return RetrievalContentType.TEXT_TRANSCRIPT;

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
                BuildReferenceTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), LocatePassage(result.Result.Location, GetRetrievalContentType(result.Result.FileType), result.Result.ChunkIndex + 1)));
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
                BuildReferenceTitle(FirstNonEmpty(result.Result.FileName, dataSource.Name), LocatePassage(result.Result.Location, GetRetrievalContentType(result.Result.FileType), result.Result.ChunkIndex + 1)));
        }
    }
}
