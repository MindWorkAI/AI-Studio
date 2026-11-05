namespace AIStudio.Tools.RAG;

/// <summary>
/// Cuts what a search found into pages, without keeping anything between two of them.
/// </summary>
/// <remarks>
/// <para>
/// Neither the vector store nor the keyword index knows an offset, and neither needs one: page p
/// of size k is cut from the first p·k + 1 matches of every channel. The one match beyond the
/// page tells whether a next page is worth asking for. The first page is therefore exactly what a
/// search for k matches always returned. A data source which cannot page that far, such as an ERI
/// data source, says so through IDataSource.GetLastPage.
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
    /// Every page fetches its whole window again, from the vector store and the keyword index. The
    /// first page is exempt: its size is what the user or the organization configured, and
    /// fetching it is what the retrieval always did.
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
    /// Cuts one page out of what two channels found, the channels taking turns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both channels make one ranking: the best match of the first channel, then the best of the
    /// second, then the second best of each, and so on. A page is the next stretch of that ranking,
    /// as many matches as the page size, whichever channel they come from. A channel which runs out
    /// of matches leaves its turns to the other one, which then fills the page on its own. So a
    /// page holds as many matches as configured, and each channel brings its best ones first.
    /// This order is deterministic on purpose; reranking would replace it, and change the first
    /// page with it.
    /// </para>
    /// <para>
    /// A match both channels found takes the first of its two places. Hence, no match turns up
    /// twice, neither on one page nor on two. Matches without a key are never taken for one
    /// another.
    /// </para>
    /// <para>
    /// The window of the page is enough to place every match up to the one beyond the page: the
    /// first n places of the ranking come from the first n matches of each channel at most, since
    /// the first n matches of one channel alone already fill n places.
    /// </para>
    /// </remarks>
    /// <param name="first">What the first channel found, the most relevant first, fetched with the window of this page.</param>
    /// <param name="second">What the second channel found, likewise.</param>
    /// <param name="getKey">What identifies a match across both channels. Letter case does not matter.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of matches per page.</param>
    /// <returns>The matches of this page, and whether the next page is worth asking for.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public static (IReadOnlyList<T> Matches, bool HasMore) Merge<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, string> getKey, int page, int pageSize)
    {
        var end = GetPageEnd(page, pageSize);
        var start = end - pageSize;

        // The one match beyond the page tells whether the next page is worth asking for:
        var ranking = TakeTurns(first, second, getKey, end + 1);
        var pageMatches = ranking.Skip(start).Take(pageSize).ToList();

        return (pageMatches, HasMore(page, pageSize, ranking.Count));
    }

    /// <summary>
    /// Joins what two channels found into one ranking, the channels taking turns.
    /// </summary>
    /// <param name="first">What the first channel found, the most relevant first.</param>
    /// <param name="second">What the second channel found, likewise.</param>
    /// <param name="getKey">What identifies a match across both channels. Letter case does not matter.</param>
    /// <param name="count">How many places of the ranking are needed.</param>
    /// <returns>The first places of the ranking, fewer than asked for only when both channels ran out.</returns>
    private static List<T> TakeTurns<T>(IReadOnlyList<T> first, IReadOnlyList<T> second, Func<T, string> getKey, int count)
    {
        var ranking = new List<T>(count);
        var placedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var rank = 0; ranking.Count < count && (rank < first.Count || rank < second.Count); rank++)
        {
            Place(first, rank);
            if (ranking.Count < count)
                Place(second, rank);
        }

        return ranking;

        void Place(IReadOnlyList<T> channel, int rank)
        {
            if (rank >= channel.Count)
                return;

            var match = channel[rank];
            var key = getKey(match);

            // The other channel, or this one further up, placed it already:
            if (!string.IsNullOrWhiteSpace(key) && !placedKeys.Add(key))
                return;

            ranking.Add(match);
        }
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
    /// A single match beyond this page is enough to ask for the next one, unless this page is the
    /// last one which can be retrieved at all.
    /// </remarks>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">The number of matches per page.</param>
    /// <param name="count">How many matches the window of this page held.</param>
    private static bool HasMore(int page, int pageSize, int count) => page < GetLastPage(pageSize) && count > page * pageSize;
}