namespace AIStudio.Tools.RAG;

/// <summary>
/// Cuts what a search found into pages, without keeping anything between two of them.
/// </summary>
/// <remarks>
/// <para>
/// Neither the vector store nor the keyword index knows an offset, and neither needs one: page p
/// of size k is cut from the first p·k + 1 matches of every channel. The one match beyond the
/// page tells whether a next page is worth asking for. The first page is therefore exactly what a
/// search for k matches always returned.
/// </para>
/// <para>
/// Staying without state is not a shortcut but a requirement: tool results do not travel into
/// later turns, so a page has to come out of the query and its number alone.
/// </para>
/// </remarks>
public static class RetrievalPaging
{
    /// <summary>
    /// How many matches a page beyond the first may fetch at most, per channel.
    /// </summary>
    /// <remarks>
    /// Every page fetches its whole window again, from the vector store and the keyword index, or
    /// from the ERI server. The first page is exempt: its size is what the user or the organization
    /// configured, and fetching it is what the retrieval always did.
    /// </remarks>
    public const int MAX_RESULT_WINDOW = 100;

    /// <summary>
    /// The last page which can be retrieved for the given page size.
    /// </summary>
    /// <param name="pageSize">The number of matches per page.</param>
    /// <returns>The number of the last page, which is at least 1.</returns>
    public static int GetLastPage(int pageSize) => pageSize < 1 ? 1 : Math.Max(1, (MAX_RESULT_WINDOW - 1) / pageSize);

    /// <summary>
    /// How many matches every channel has to deliver for the given page.
    /// </summary>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of matches per page.</param>
    /// <returns>The size of the window, i.e., the page, all pages before it, and one match more.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public static int GetWindowSize(int page, int pageSize) => GetPageEnd(page, pageSize) + 1;

    /// <summary>
    /// Cuts one page out of what a single channel found.
    /// </summary>
    /// <param name="matches">What the channel found, the most relevant first, fetched with the window of this page.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of matches per page.</param>
    /// <returns>The matches of this page, and whether the next page is worth asking for.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public static (IReadOnlyList<T> Matches, bool HasMore) Cut<T>(IReadOnlyList<T> matches, int page, int pageSize)
    {
        var end = GetPageEnd(page, pageSize);
        var start = end - pageSize;
        var pageMatches = matches.Skip(start).Take(pageSize).ToList();

        return (pageMatches, HasMore(page, pageSize, matches.Count));
    }

    /// <summary>
    /// Cuts one page out of what two channels found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page holds the page of the first channel, followed by the page of the second one. This
    /// order is deterministic on purpose; reranking would replace it, and change the first page
    /// with it.
    /// </para>
    /// <para>
    /// A match both channels found is shown once, on the earlier of its two pages; on the same
    /// page, in the part of the first channel. Hence, no match turns up on two pages. Matches
    /// without a key are never taken for one another.
    /// </para>
    /// </remarks>
    /// <param name="first">What the first channel found, the most relevant first, fetched with the window of this page.</param>
    /// <param name="second">What the second channel found, likewise.</param>
    /// <param name="getKey">What identifies a match across both channels. Letter case does not matter.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of matches per page and channel.</param>
    /// <returns>The matches of this page, and whether the next page is worth asking for.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public static (IReadOnlyList<T> Matches, bool HasMore) Merge<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, string> getKey, int page, int pageSize)
    {
        var end = GetPageEnd(page, pageSize);
        var start = end - pageSize;
        var firstRanks = GetFirstRanks(first, end + 1, getKey);
        var secondRanks = GetFirstRanks(second, end + 1, getKey);
        var pageMatches = new List<T>(2 * pageSize);

        for (var rank = start; rank < Math.Min(end, first.Count); rank++)
        {
            var match = first[rank];
            var key = getKey(match);
            if (!string.IsNullOrWhiteSpace(key))
            {
                // The first channel found it further up already:
                if (firstRanks[key] != rank)
                    continue;

                // The second channel showed it on an earlier page:
                if (secondRanks.TryGetValue(key, out var secondRank) && secondRank < start)
                    continue;
            }

            pageMatches.Add(match);
        }

        for (var rank = start; rank < Math.Min(end, second.Count); rank++)
        {
            var match = second[rank];
            var key = getKey(match);
            if (!string.IsNullOrWhiteSpace(key))
            {
                // The second channel found it further up already:
                if (secondRanks[key] != rank)
                    continue;

                // The first channel shows it on this page or showed it on an earlier one:
                if (firstRanks.TryGetValue(key, out var firstRank) && firstRank < end)
                    continue;
            }

            pageMatches.Add(match);
        }

        return (pageMatches, HasMore(page, pageSize, first.Count, second.Count));
    }

    /// <summary>
    /// Where the given page ends, i.e., the number of matches on it and on all pages before it.
    /// </summary>
    private static int GetPageEnd(int page, int pageSize)
    {
        var lastPage = GetLastPage(pageSize);
        if (page < 1 || page > lastPage)
            throw new ArgumentOutOfRangeException(nameof(page), page, $"With {pageSize} matches per page, the page has to be between 1 and {lastPage}.");

        return page * Math.Max(0, pageSize);
    }

    /// <remarks>
    /// Whatever a channel found beyond this page is enough to ask for the next one. That page can
    /// still turn out empty, when the other channel showed all of it before. Saying there is more
    /// when there is not costs one empty page; saying the opposite would hide matches.
    /// </remarks>
    private static bool HasMore(int page, int pageSize, params int[] channelCounts)
    {
        if (page >= GetLastPage(pageSize))
            return false;

        var end = page * pageSize;
        return channelCounts.Any(count => count > end);
    }

    private static Dictionary<string, int> GetFirstRanks<T>(IReadOnlyList<T> matches, int window, Func<T, string> getKey)
    {
        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var rank = 0; rank < Math.Min(window, matches.Count); rank++)
        {
            var key = getKey(matches[rank]);
            if (!string.IsNullOrWhiteSpace(key))
                ranks.TryAdd(key, rank);
        }

        return ranks;
    }
}