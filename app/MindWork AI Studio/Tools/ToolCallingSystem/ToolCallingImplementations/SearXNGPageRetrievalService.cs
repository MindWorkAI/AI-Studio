using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal sealed class SearXNGPageRetrievalService(WebPageRetrievalService webPageRetrievalService)
{
    private const int MAX_PARALLEL_RETRIEVALS = 4;

    public async Task<WebSearchPageRetrievalResult> RetrieveAsync(
        IReadOnlyList<SearchCandidate> candidates,
        int pageTimeoutSeconds,
        int retrievalTimeoutSeconds,
        int maxTotalContentCharacters,
        int minContentCharactersPerResult,
        CancellationToken token)
    {
        var attemptedCount = 0;
        var blockedCount = 0;
        var pageTimedOutCount = 0;
        var failedCount = 0;
        var emptyContentCount = 0;
        var retrievalTimedOut = 0;
        using var retrievalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        retrievalTimeoutCts.CancelAfter(TimeSpan.FromSeconds(retrievalTimeoutSeconds));
        using var retrievalSemaphore = new SemaphoreSlim(MAX_PARALLEL_RETRIEVALS);

        async Task<RetrievedSearchPage?> RetrieveCandidateAsync(SearchCandidate candidate)
        {
            var enteredSemaphore = false;
            try
            {
                await retrievalSemaphore.WaitAsync(retrievalTimeoutCts.Token);
                enteredSemaphore = true;
                Interlocked.Increment(ref attemptedCount);
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
                    Interlocked.Increment(ref emptyContentCount);
                    return null;
                }

                return new RetrievedSearchPage(candidate, retrievedPage);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref retrievalTimedOut, 1);
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (WebPageAccessBlockedException)
            {
                Interlocked.Increment(ref blockedCount);
                return null;
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref pageTimedOutCount);
                return null;
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref failedCount);
                return null;
            }
            finally
            {
                if (enteredSemaphore)
                    retrievalSemaphore.Release();
            }
        }

        var retrievedPages = await Task.WhenAll(candidates.Select(RetrieveCandidateAsync));
        token.ThrowIfCancellationRequested();
        var mergedResults = MergeFinalUrlDuplicates(retrievedPages.OfType<RetrievedSearchPage>());
        ApplyContentBudget(mergedResults, maxTotalContentCharacters, minContentCharactersPerResult);
        var statistics = new WebSearchPageRetrievalStatistics(
            attemptedCount,
            blockedCount,
            pageTimedOutCount,
            failedCount,
            emptyContentCount);
        return new WebSearchPageRetrievalResult(mergedResults, retrievalTimedOut == 1, statistics);
    }

    private static List<WebSearchPageResult> MergeFinalUrlDuplicates(IEnumerable<RetrievedSearchPage> retrievedPages) => retrievedPages
        .GroupBy(result => SearXNGSearchClient.NormalizeUrl(result.RetrievedPage.Page.FinalUrl), StringComparer.Ordinal)
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
}

internal sealed record WebSearchPageRetrievalResult(
    IReadOnlyList<WebSearchPageResult> Results,
    bool RetrievalTimedOut,
    WebSearchPageRetrievalStatistics ErrorStatistics);

internal sealed record WebSearchPageRetrievalStatistics(
    int AttemptedCount,
    int BlockedCount,
    int PageTimedOutCount,
    int FailedCount,
    int EmptyContentCount);

internal sealed class WebSearchPageResult(SearchCandidate candidate, RetrievedWebPage retrievedPage)
{
    public SearchCandidate Candidate { get; } = candidate;

    public RetrievedWebPage RetrievedPage { get; } = retrievedPage;

    public string ReturnedMarkdown { get; set; } = string.Empty;

    public bool ContentTruncated { get; set; }
}
