namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

internal sealed class SearchCandidate
{
    public required int Rank { get; set; }

    public required Uri RetrievalUrl { get; set; }

    public required List<string> OriginalUrls { get; init; }

    public required string Title { get; set; }

    public required string Snippet { get; set; }

    public required string PublishedDate { get; set; }

    public SearchCandidate Clone() => new()
    {
        Rank = this.Rank,
        RetrievalUrl = this.RetrievalUrl,
        OriginalUrls = [..this.OriginalUrls],
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
            this.Title = SearXNGSearchClient.FirstNonEmpty(this.Title, candidate.Title);
            this.Snippet = SearXNGSearchClient.FirstNonEmpty(this.Snippet, candidate.Snippet);
            this.PublishedDate = SearXNGSearchClient.FirstNonEmpty(this.PublishedDate, candidate.PublishedDate);
        }

        AddDistinct(this.OriginalUrls, candidate.OriginalUrls, StringComparer.Ordinal);
    }

    private static void AddDistinct(List<string> target, IEnumerable<string> values, StringComparer comparer)
    {
        foreach (var value in values)
        {
            if (!target.Contains(value, comparer))
                target.Add(value);
        }
    }
}