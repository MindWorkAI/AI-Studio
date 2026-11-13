namespace AIStudio.Tools.RAG;

/// <summary>
/// What kept one retrieval from covering the whole data source.
/// </summary>
/// <param name="queryWrittenByUser">Whether the query is the user's own message, which decides who hears about its problems.</param>
public sealed class RetrievalRun(bool queryWrittenByUser)
{
    // Both channels search at the same time:
    private readonly Lock gapLock = new();
    private readonly HashSet<RetrievalGap> gaps = [];

    public bool QueryWrittenByUser => queryWrittenByUser;

    public void Add(RetrievalGap gap)
    {
        lock (this.gapLock)
            this.gaps.Add(gap);
    }

    public IReadOnlyList<RetrievalGap> GetGaps()
    {
        lock (this.gapLock)
            return this.gaps.Order().ToList();
    }
}