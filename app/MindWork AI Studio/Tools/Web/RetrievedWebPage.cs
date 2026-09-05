using AIStudio.Provider;

namespace AIStudio.Tools.Web;

public sealed class RetrievedWebPage
{
    public required HTMLParserWebPage Page { get; init; }

    public required ExtractedWebPage ExtractedPage { get; init; }

    public required DateTimeOffset RetrievedAtUtc { get; init; }

    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;
}