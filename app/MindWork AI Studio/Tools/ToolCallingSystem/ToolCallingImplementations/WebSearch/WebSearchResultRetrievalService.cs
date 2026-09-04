using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

internal sealed class WebSearchResultRetrievalService(WebPageRetrievalService webPageRetrievalService)
{
    private const int MAX_PARALLEL_RETRIEVALS = 4;

    /// <summary>
    /// How much of a search service's snippet a hit without a readable page may return.
    /// </summary>
    /// <remarks>
    /// A snippet is a sentence or two by design, so this limit is never reached by a service
    /// behaving as documented. It exists so that one that does not cannot smuggle text past the
    /// content budget, which is a setting the user made and which snippets do not draw from.
    /// </remarks>
    private const int MAX_SNIPPET_CHARACTERS = 1000;

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
        var retrievalTasks = new List<Task<WebSearchPageResult>>(candidates.Count);
        foreach (var candidate in candidates)
            retrievalTasks.Add(this.RetrieveCandidateAsync(candidate, pageTimeoutSeconds, retrievalSemaphore, retrievalTimeoutCts, counters, token));

        var retrievedPages = await Task.WhenAll(retrievalTasks);
        token.ThrowIfCancellationRequested();
        var mergedResults = MergeDuplicates(retrievedPages);
        ApplySnippets(mergedResults);
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
    /// visible: nothing here outlives the call that hands them over.<br/><br/>
    /// Every candidate comes back, whether or not its page could be read. A hit the search
    /// service found is worth reporting even without its content: the model can still name the
    /// page as a place to look, and the snippet often answers the question by itself. Only a
    /// candidate that has nothing left to say is dropped, and that is decided after merging.
    /// </remarks>
    private async Task<WebSearchPageResult> RetrieveCandidateAsync(
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
                return new(candidate, null, WebSearchPageRetrievalOutcome.NO_READABLE_CONTENT);
            }

            return new(candidate, retrievedPage, WebSearchPageRetrievalOutcome.RETRIEVED);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Interlocked.Exchange(ref counters.RetrievalTimedOut, 1);
            return new(candidate, null, WebSearchPageRetrievalOutcome.RETRIEVAL_TIMED_OUT);
        }
        catch (WebPageAccessBlockedException)
        {
            Interlocked.Increment(ref counters.Blocked);
            return new(candidate, null, WebSearchPageRetrievalOutcome.BLOCKED);
        }
        catch (TimeoutException)
        {
            Interlocked.Increment(ref counters.PageTimedOut);
            return new(candidate, null, WebSearchPageRetrievalOutcome.PAGE_TIMED_OUT);
        }
        catch (InvalidOperationException)
        {
            Interlocked.Increment(ref counters.Failed);
            return new(candidate, null, WebSearchPageRetrievalOutcome.FAILED);
        }
        finally
        {
            if (enteredSemaphore)
                retrievalSemaphore.Release();
        }
    }

    private static List<WebSearchPageResult> MergeDuplicates(IEnumerable<WebSearchPageResult> results) => results
        .GroupBy(result => SearchCandidate.NormalizeUrl(result.CitationUrl), StringComparer.Ordinal)
        .Select(group =>
        {
            //
            // A page that was read carries its group even when a hit without one ranked better.
            // The group is one page, and letting a snippet win would throw away the only thing
            // the retrieval accomplished. Its rank and reported title still come from the best
            // hit of the group, because that is what Merge takes from whichever ranked highest.
            //
            var rankedGroup = group.OrderByDescending(result => result.HasPageContent).ThenBy(result => result.Candidate.Rank).ToList();
            var carrier = rankedGroup[0];
            var metadata = carrier.Candidate.Clone();
            foreach (var duplicate in rankedGroup.Skip(1))
                metadata.Merge(duplicate.Candidate);

            return new WebSearchPageResult(metadata, carrier.RetrievedPage, carrier.Outcome);
        })
        .Where(HasSomethingToReport)
        .OrderBy(result => result.Candidate.Rank)
        .ToList();

    /// <summary>
    /// Whether this hit still tells the model something once its page turned out to be
    /// unreadable.
    /// </summary>
    /// <remarks>
    /// Decided after merging, because a hit two services found may owe its title to one of them
    /// and its snippet to the other. What remains here is a bare URL with no title and no
    /// snippet, which costs tokens and says nothing, so it is dropped as it always was.
    /// </remarks>
    private static bool HasSomethingToReport(WebSearchPageResult result) =>
        result.HasPageContent ||
        !string.IsNullOrWhiteSpace(result.Candidate.Snippet) ||
        !string.IsNullOrWhiteSpace(result.Candidate.Title);

    /// <summary>
    /// Puts the search service's snippet in place of the content of every page that could not
    /// be read.
    /// </summary>
    private static void ApplySnippets(List<WebSearchPageResult> results)
    {
        foreach (var result in results)
        {
            if (result.HasPageContent)
                continue;

            var snippet = result.Candidate.Snippet;
            result.ReturnedMarkdown = snippet.Length <= MAX_SNIPPET_CHARACTERS ? snippet : $"{snippet[..(MAX_SNIPPET_CHARACTERS - 1)].TrimEnd()}…";
        }
    }

    /// <summary>
    /// Shares the content budget between the pages that were read.
    /// </summary>
    /// <remarks>
    /// Only they take part in it. The budget exists so that a few long pages do not crowd each
    /// other out, and a hit returning a snippet has nothing to crowd with: reserving the
    /// per-result minimum for it would let a page nobody could read shorten one somebody can.
    /// The snippets are capped on their own instead.
    /// </remarks>
    private static void ApplyContentBudget(List<WebSearchPageResult> results, int maxTotalContentCharacters, int minContentCharactersPerResult)
    {
        var pageResults = results.Where(result => result.HasPageContent).ToList();
        var remainingBudget = maxTotalContentCharacters;
        for (var index = 0; index < pageResults.Count; index++)
        {
            var result = pageResults[index];
            var originalMarkdown = result.RetrievedPage!.ExtractedPage.Markdown;
            var remainingResults = pageResults.Count - index - 1;
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

    /// <summary>
    /// What became of the pages of one search, counted while they are fetched in parallel.
    /// </summary>
    /// <remarks>
    /// Public fields rather than properties, because the retrievals count through Interlocked,
    /// which needs a reference to the storage itself.<br/><br/>
    /// These count retrievals, while the outcome on each result describes one hit. The two do
    /// not have to agree: two hits leading to the same page are two retrievals and one result.
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