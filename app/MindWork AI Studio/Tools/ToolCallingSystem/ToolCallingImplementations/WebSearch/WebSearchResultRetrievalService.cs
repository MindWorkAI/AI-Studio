using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

internal sealed class WebSearchResultRetrievalService(WebPageRetrievalService webPageRetrievalService)
{
    private const int MAX_PARALLEL_RETRIEVALS = 4;

    public async Task<WebSearchPageRetrievalResult> RetrieveAsync(
        IReadOnlyList<SearchCandidate> candidates,
        int pageTimeoutSeconds,
        int allPagesRetrievalTimeoutSeconds,
        int maxTotalContentCharacters,
        int minContentCharactersPerResult,
        CancellationToken token)
    {
        var counters = new RetrievalCounters();
        using var retrievalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        retrievalTimeoutCts.CancelAfter(TimeSpan.FromSeconds(allPagesRetrievalTimeoutSeconds));
        using var retrievalSemaphore = new SemaphoreSlim(MAX_PARALLEL_RETRIEVALS);

        //
        // Started in a loop rather than through a Select: a lambda would capture the semaphore and
        // the timeout source, and a captured disposable outliving its scope is exactly what one
        // cannot see from the call site. Handing them over as arguments keeps that impossible.
        //
        var retrievalTasks = new List<Task<RetrievedSearchPage?>>(candidates.Count);
        foreach (var candidate in candidates)
            retrievalTasks.Add(this.RetrieveCandidateAsync(candidate, pageTimeoutSeconds, retrievalSemaphore, retrievalTimeoutCts, counters, token));

        var retrievedPages = await Task.WhenAll(retrievalTasks);
        token.ThrowIfCancellationRequested();
        var mergedResults = MergeFinalUrlDuplicates(retrievedPages.OfType<RetrievedSearchPage>());
        ApplyContentBudget(mergedResults, maxTotalContentCharacters, minContentCharactersPerResult);
        var statistics = new WebSearchPageRetrievalStatistics(
            counters.Attempted,
            counters.Blocked,
            counters.PageTimedOut,
            counters.Failed,
            counters.EmptyContent);

        return new WebSearchPageRetrievalResult(mergedResults, counters.RetrievalTimedOut == 1, statistics);
    }

    /// <summary>
    /// Retrieves one search result page, counting how it went.
    /// </summary>
    /// <remarks>
    /// The semaphore and the timeout source belong to the caller, which disposes them once every
    /// retrieval has finished. Passing them in rather than capturing them keeps that ownership
    /// visible: nothing here outlives the call that hands them over.
    /// </remarks>
    private async Task<RetrievedSearchPage?> RetrieveCandidateAsync(
        SearchCandidate candidate,
        int pageTimeoutSeconds,
        SemaphoreSlim retrievalSemaphore,
        CancellationTokenSource retrievalTimeoutCts,
        RetrievalCounters counters,
        CancellationToken token)
    {
        var enteredSemaphore = false;
        try
        {
            await retrievalSemaphore.WaitAsync(retrievalTimeoutCts.Token);
            enteredSemaphore = true;
            Interlocked.Increment(ref counters.Attempted);
            var retrievedPage = await webPageRetrievalService.RetrieveAsync(
                candidate.RetrievalUrl,
                new WebPageRetrievalOptions
                {
                    TimeoutSeconds = pageTimeoutSeconds,
                    PublicTargetsOnly = true,
                },
                retrievalTimeoutCts.Token);
            if (string.IsNullOrWhiteSpace(retrievedPage.ExtractedPage.Markdown))
            {
                Interlocked.Increment(ref counters.EmptyContent);
                return null;
            }

            return new RetrievedSearchPage(candidate, retrievedPage);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Interlocked.Exchange(ref counters.RetrievalTimedOut, 1);
            return null;
        }
        catch (WebPageAccessBlockedException)
        {
            Interlocked.Increment(ref counters.Blocked);
            return null;
        }
        catch (TimeoutException)
        {
            Interlocked.Increment(ref counters.PageTimedOut);
            return null;
        }
        catch (InvalidOperationException)
        {
            Interlocked.Increment(ref counters.Failed);
            return null;
        }
        finally
        {
            if (enteredSemaphore)
                retrievalSemaphore.Release();
        }
    }

    private static List<WebSearchPageResult> MergeFinalUrlDuplicates(IEnumerable<RetrievedSearchPage> retrievedPages) => retrievedPages
        .GroupBy(result => SearchCandidate.NormalizeUrl(result.RetrievedPage.Page.FinalUrl), StringComparer.Ordinal)
        .Select(group =>
        {
            var rankedGroup = group.OrderBy(result => result.Candidate.Rank).ToList();
            var metadata = rankedGroup[0].Candidate.Clone();
            foreach (var duplicate in rankedGroup.Skip(1))
                metadata.Merge(duplicate.Candidate);

            return new WebSearchPageResult(metadata, rankedGroup[0].RetrievedPage);
        })
        .OrderBy(result => result.Candidate.Rank)
        .ToList();

    private static void ApplyContentBudget(List<WebSearchPageResult> results, int maxTotalContentCharacters, int minContentCharactersPerResult)
    {
        var remainingBudget = maxTotalContentCharacters;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var originalMarkdown = result.RetrievedPage.ExtractedPage.Markdown;
            var remainingResults = results.Count - index - 1;
            var currentBudget = remainingBudget - minContentCharactersPerResult * remainingResults;
            if (originalMarkdown.Length > currentBudget)
            {
                result.ReturnedMarkdown = MarkdownTruncator.Truncate(originalMarkdown, currentBudget);
                result.ContentTruncated = true;
            }
            else
            {
                result.ReturnedMarkdown = originalMarkdown;
            }

            remainingBudget -= result.ReturnedMarkdown.Length;
        }
    }

    private sealed record RetrievedSearchPage(SearchCandidate Candidate, RetrievedWebPage RetrievedPage);

    /// <summary>
    /// What became of the pages of one search, counted while they are fetched in parallel.
    /// </summary>
    /// <remarks>
    /// Public fields rather than properties, because the retrievals count through Interlocked,
    /// which needs a reference to the storage itself.
    /// </remarks>
    private sealed class RetrievalCounters
    {
        public int Attempted;
        public int Blocked;
        public int PageTimedOut;
        public int Failed;
        public int EmptyContent;
        public int RetrievalTimedOut;
    }
}