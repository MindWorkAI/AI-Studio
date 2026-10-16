namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// Turns the hits of a search service into the candidates the tool works with.
/// </summary>
/// <remarks>
/// Every backend needs the same four things here: keep the service's ranking, drop hits whose
/// URL cannot be retrieved, merge hits pointing at the same page, and stop at the limit the
/// tool set. Doing it once means a new backend only has to say what its hits look like.
/// </remarks>
internal static class SearchCandidateCollector
{
    /// <summary>
    /// Collects the hits into ranked candidates.
    /// </summary>
    /// <param name="backend">The search service the hits came from.</param>
    /// <param name="hits">The hits, in the order the search service ranked them.</param>
    /// <param name="limit">The most hits to use.</param>
    /// <param name="candidateCount">How many hits were used, before equivalent URLs were merged.</param>
    /// <returns>The candidates, ordered by rank.</returns>
    public static IReadOnlyList<SearchCandidate> Collect(WebSearchBackend backend, IEnumerable<SearchHit> hits, int limit, out int candidateCount)
    {
        var rankedHits = hits.Take(limit).ToList();

        //
        // Counted before the hits are filtered and merged, because this number answers a
        // different question than the candidate list does: whether the search found anything at
        // all. A search whose every hit was unusable is a matter of the pages, not of the query.
        //
        candidateCount = rankedHits.Count;

        var candidatesByUrl = new Dictionary<string, SearchCandidate>(StringComparer.Ordinal);
        for (var index = 0; index < rankedHits.Count; index++)
        {
            var hit = rankedHits[index];
            if (!Uri.TryCreate(hit.Url, UriKind.Absolute, out var url) || url is not { Scheme: "http" or "https" })
                continue;

            //
            // The fragment addresses a place inside the page. A server never sees it, and
            // keeping it would make two links to the same page look like two pages:
            //
            var retrievalUrl = RemoveFragment(url);
            var candidate = new SearchCandidate
            {
                Rank = index + 1,
                RetrievalUrl = retrievalUrl,
                OriginalUrls = [hit.Url],
                Backends = [backend],
                Title = hit.Title,
                Snippet = hit.Snippet,
                PublishedDate = hit.PublishedDate,
            };

            var normalizedUrl = SearchCandidate.NormalizeUrl(retrievalUrl);
            if (candidatesByUrl.TryGetValue(normalizedUrl, out var existingCandidate))
                existingCandidate.Merge(candidate);
            else
                candidatesByUrl[normalizedUrl] = candidate;
        }

        return candidatesByUrl.Values
            .OrderBy(candidate => candidate.Rank)
            .ToList();
    }

    private static Uri RemoveFragment(Uri url) => new UriBuilder(url)
    {
        Fragment = string.Empty,
    }.Uri;
}