namespace AIStudio.Tools.RAG;

/// <summary>
/// Cuts results into pages by their length, without leaving any of them out.
/// </summary>
/// <remarks>
/// <para>
/// A page ends right before the first result which no longer fits into it, and the next page
/// starts with that result. No result makes room for a shorter one after it, so the order stays as
/// it is: every result turns up on exactly one page, and the pages before it hold every result
/// ranked above it.
/// </para>
/// <para>
/// The cut depends on the lengths alone. Whoever pages through results without keeping anything
/// between two pages, such as Semantic Search, can therefore cut them anew for every page and gets
/// the same pages each time.
/// </para>
/// </remarks>
public static class CharacterPaging
{
    /// <summary>
    /// Cuts results of the given lengths into pages.
    /// </summary>
    /// <remarks>
    /// Every page holds at least one result: a result longer than the budget on its own fills a
    /// page of its own, rather than holding up all results after it. Whoever must stay within the
    /// budget shortens such a result before.
    /// </remarks>
    /// <param name="lengths">The length of each result in characters, in the order they are shown.</param>
    /// <param name="budget">How many characters a page may hold.</param>
    /// <returns>The pages, in order; none without results.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The budget is below 1, or a length is negative.</exception>
    public static IReadOnlyList<CharacterPage> Cut(IReadOnlyList<int> lengths, int budget)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(budget, 1);

        var pages = new List<CharacterPage>();
        var start = 0;
        var pageLength = 0L;
        for (var index = 0; index < lengths.Count; index++)
        {
            var length = lengths[index];
            ArgumentOutOfRangeException.ThrowIfNegative(length, nameof(lengths));

            // The page ends before the first result which no longer fits, unless it would stay empty:
            if (index > start && pageLength + length > budget)
            {
                pages.Add(new(start, index - start));
                start = index;
                pageLength = 0;
            }

            pageLength += length;
        }

        if (start < lengths.Count)
            pages.Add(new(start, lengths.Count - start));

        return pages;
    }
}