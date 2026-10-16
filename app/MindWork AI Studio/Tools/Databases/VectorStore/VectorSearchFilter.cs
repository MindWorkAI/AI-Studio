namespace AIStudio.Tools.Databases.VectorStore;

/// <summary>
/// Restricts a vector search to some of the points in a store, e.g., to the chunks of those mails
/// which match the other conditions of a search.
/// </summary>
/// <remarks>
/// A filter without any point matches nothing, and a search with it finds nothing. It never turns
/// into a search of the whole store: a caller whose other conditions matched nothing must not get
/// back exactly what those conditions ruled out.
///
/// Up to MAX_POINT_IDS_PER_REQUEST ids travel along with the search, and the vector store looks at
/// those points only. A longer list would make every single request large, so the search then asks
/// for OVERSAMPLING_FACTOR times as many matches without a restriction and drops the foreign ones
/// afterward. That can leave fewer matches than asked for, when the filter covers only a small part
/// of the store.
///
/// Either way, the matches are checked against the filter once more before anybody sees them. A
/// filtered search therefore never returns a point outside the filter, whatever the vector store did.
/// </remarks>
/// <param name="pointIds">The ids of the points the search may return. Each has to be a GUID.</param>
/// <exception cref="FormatException">One of the ids is not a GUID.</exception>
public sealed class VectorSearchFilter(IEnumerable<string> pointIds)
{
    /// <summary>
    /// The most point ids which are sent along with one search.
    /// </summary>
    internal const int MAX_POINT_IDS_PER_REQUEST = 10_000;

    /// <summary>
    /// How many more matches a search asks for when the filter is too long to be sent along.
    /// </summary>
    internal const int OVERSAMPLING_FACTOR = 4;

    private readonly HashSet<Guid> pointIds = pointIds.Select(Guid.Parse).ToHashSet();

    /// <summary>
    /// Whether no point at all passes the filter, so there is nothing to search.
    /// </summary>
    public bool MatchesNothing => this.pointIds.Count == 0;

    private bool IsSentAlong => this.pointIds.Count <= MAX_POINT_IDS_PER_REQUEST;

    /// <summary>
    /// The point ids to send along with the search.
    /// </summary>
    /// <returns>The ids, or null when there are too many and the matches get filtered afterward.</returns>
    public IReadOnlyList<string>? GetRequestPointIds() => this.IsSentAlong ? this.pointIds.Select(id => id.ToString()).ToList() : null;

    /// <summary>
    /// How many matches to ask the vector store for.
    /// </summary>
    /// <param name="maxMatches">How many matches the caller wants.</param>
    /// <returns>The number of matches to ask for.</returns>
    public int GetCandidateCount(int maxMatches) => this.IsSentAlong ? maxMatches : (int)Math.Min((long)maxMatches * OVERSAMPLING_FACTOR, int.MaxValue);

    /// <summary>
    /// Keeps the matches which pass the filter, in their order.
    /// </summary>
    /// <param name="candidates">The matches of the vector store, best first.</param>
    /// <param name="maxMatches">How many matches the caller wants.</param>
    /// <returns>At most maxMatches matches, all of them inside the filter.</returns>
    public IReadOnlyList<VectorSearchResult> Apply(IEnumerable<VectorSearchResult> candidates, int maxMatches) =>
        candidates.Where(this.Contains).Take(maxMatches).ToList();

    private bool Contains(VectorSearchResult candidate) => Guid.TryParse(candidate.PointId, out var pointId) && this.pointIds.Contains(pointId);
}