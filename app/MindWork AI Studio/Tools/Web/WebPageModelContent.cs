namespace AIStudio.Tools.Web;

/// <summary>
/// The parts of a retrieved web page that a tool hands to a model.
/// </summary>
/// <remarks>
/// Every field here left the page as free text, so every field can carry an injection: a title,
/// an author name from a meta tag, and a publication date are all attacker-controlled on a page
/// the model asked for. They are filtered together with the page content.
/// </remarks>
public sealed record WebPageModelContent(string Markdown, string Title, string Description, IReadOnlyList<string> Authors, string Language, string PublishedTime, string ModifiedTime)
{
    /// <summary>
    /// Takes the model-facing fields of an extracted page, with the content the tool decided to
    /// return, which may be shorter than what was extracted.
    /// </summary>
    public static WebPageModelContent From(ExtractedWebPage page, string markdown) => new(markdown, page.Title, page.Description, page.Authors, page.Language, page.PublishedTime, page.ModifiedTime);
}