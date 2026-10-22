namespace AIStudio.Tools.Web;

public sealed class ExtractedWebPage
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> Authors { get; init; }

    public required string PublishedTime { get; init; }

    public required string ModifiedTime { get; init; }

    public required string Language { get; init; }

    public required string SiteName { get; init; }

    public required Uri? CanonicalUrl { get; init; }

    public required string Markdown { get; init; }

    public required IReadOnlyList<string> Outline { get; init; }
}