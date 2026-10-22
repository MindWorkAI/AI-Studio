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

    /// <summary>
    /// Whether the page was read from a non-public address, such as one in the intranet or behind
    /// a VPN.
    /// </summary>
    /// <remarks>
    /// Only the request which returned the page counts, the last one after every redirect: it is
    /// where the content came from. A public address which redirects into the intranet therefore
    /// counts, and an intranet address which redirects to a public page, such as a sign-in page,
    /// does not.<br/><br/>
    /// The content of such a page is internal, so a caller which hands it to a model has to keep
    /// it from leaving the organization. Stated here rather than read from
    /// RequiredProviderConfidence, which answers another question: it counts every request, so the
    /// address of an intranet page does not reach a less trusted provider either.
    /// </remarks>
    public bool ReachedNonPublicAddress { get; init; }
}