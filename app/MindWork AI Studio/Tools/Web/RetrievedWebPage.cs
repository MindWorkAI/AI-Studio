using AIStudio.Provider;

namespace AIStudio.Tools.Web;

/// <summary>
/// A web page or text document as the retrieval service read it.
/// </summary>
/// <remarks>
/// Nothing here is filtered for prompt injections yet. Everything a caller hands on to a model
/// has to go through the WebPageContentSanitizer or the PromptInjectionGuardService first, after
/// it was cut down to what the model actually gets: only that part needs checking, and a page
/// can be far larger.
/// </remarks>
public sealed class RetrievedWebPage
{
    public required HTMLParserWebPage Page { get; init; }

    /// <summary>
    /// Whether the content was extracted from an HTML page or is the text of a document.
    /// </summary>
    public required WebContentKind ContentKind { get; init; }

    public required ExtractedWebPage ExtractedPage { get; init; }

    public required DateTimeOffset RetrievedAtUtc { get; init; }

    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;
}