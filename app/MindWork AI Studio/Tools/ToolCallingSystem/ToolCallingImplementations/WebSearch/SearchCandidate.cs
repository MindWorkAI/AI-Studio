namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

public sealed class SearchCandidate
{
    public required int Rank { get; set; }

    public required Uri RetrievalUrl { get; set; }

    public required List<string> OriginalUrls { get; init; }

    /// <summary>
    /// The search services this hit came from.
    /// </summary>
    /// <remarks>
    /// A list rather than a single service, because two of them asked at once can return the
    /// same page, and then the merged candidate is a hit both of them found. That is worth
    /// reporting: it says more about the page than either service does alone.
    /// </remarks>
    public required List<WebSearchBackend> Backends { get; init; }

    public required string Title { get; set; }

    public required string Snippet { get; set; }

    public required string PublishedDate { get; set; }

    public SearchCandidate Clone() => new()
    {
        Rank = this.Rank,
        RetrievalUrl = this.RetrievalUrl,
        OriginalUrls = [..this.OriginalUrls],
        Backends = [..this.Backends],
        Title = this.Title,
        Snippet = this.Snippet,
        PublishedDate = this.PublishedDate,
    };

    public void Merge(SearchCandidate candidate)
    {
        if (candidate.Rank < this.Rank)
        {
            this.Rank = candidate.Rank;
            this.RetrievalUrl = candidate.RetrievalUrl;
            this.Title = candidate.Title;
            this.Snippet = candidate.Snippet;
            this.PublishedDate = candidate.PublishedDate;
        }
        else
        {
            this.Title = FirstNonEmpty(this.Title, candidate.Title);
            this.Snippet = FirstNonEmpty(this.Snippet, candidate.Snippet);
            this.PublishedDate = FirstNonEmpty(this.PublishedDate, candidate.PublishedDate);
        }

        AddDistinct(this.OriginalUrls, candidate.OriginalUrls, StringComparer.Ordinal);
        AddDistinct(this.Backends, candidate.Backends);
    }

    /// <summary>
    /// The form of a URL two candidates are compared by.
    /// </summary>
    /// <remarks>
    /// Host casing and a trailing dot are the same address to a server but different strings,
    /// and a default port may be spelled out or left out. Comparing the raw URLs would let
    /// the same page through twice and cost a second page retrieval for it.<br/><br/>
    /// This lives with the candidate rather than with a search backend, because the tool
    /// compares hits from different backends by it as well.
    /// </remarks>
    internal static string NormalizeUrl(Uri url)
    {
        var scheme = url.Scheme.ToLowerInvariant();
        var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        var port = url.IsDefaultPort ? string.Empty : $":{url.Port}";
        var userInfo = string.IsNullOrEmpty(url.UserInfo) ? string.Empty : $"{url.UserInfo}@";
        return $"{scheme}://{userInfo}{host}{port}{url.AbsolutePath}{url.Query}";
    }

    /// <summary>
    /// The first value that carries something, for fields a search hit may leave empty.
    /// </summary>
    internal static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void AddDistinct<T>(List<T> target, IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
    {
        foreach (var value in values)
        {
            if (!target.Contains(value, comparer))
                target.Add(value);
        }
    }
}