using AIStudio.Tools.Security;

namespace AIStudio.Tools.Web;

/// <summary>
/// The parts of a retrieved web page that a tool hands to a model.
/// </summary>
/// <remarks>
/// Every field here left the page as free text, so every field can carry an injection: a title,
/// an author name from a meta tag, and a publication date are all attacker-controlled on a page
/// the model asked for. They are filtered together with the page content.
/// </remarks>
public sealed record WebPageModelContent(
    string Markdown,
    string Title,
    string Description,
    IReadOnlyList<string> Authors,
    string Language,
    string PublishedTime,
    string ModifiedTime)
{
    /// <summary>
    /// Takes the model-facing fields of an extracted page, with the content the tool decided to
    /// return, which may be shorter than what was extracted.
    /// </summary>
    public static WebPageModelContent From(ExtractedWebPage page, string markdown) => new(
        markdown,
        page.Title,
        page.Description,
        page.Authors,
        page.Language,
        page.PublishedTime,
        page.ModifiedTime);
}

/// <summary>
/// Filters prompt injections out of the web page content a tool returns to a model.
/// </summary>
/// <remarks>
/// Web search and reading a single page share this: both hand the model text they fetched from
/// the public web, and neither may pass it on unchecked. All pages of one tool call are filtered
/// in a single runtime request, so a search across five pages costs one round trip and produces
/// one report for the user.
/// </remarks>
public static class WebPageContentSanitizer
{
    /// <summary>
    /// How many single-value fields each page contributes, in the order they are collected. The
    /// author list follows them and varies in length, so rebuilding depends on this being right.
    /// </summary>
    private const int SINGLE_VALUE_FIELD_COUNT = 6;

    /// <summary>
    /// Filters every model-facing text of the given pages.
    /// </summary>
    /// <param name="guardService">The guard service performing the filtering.</param>
    /// <param name="pages">The page contents to filter, each with the source it came from.</param>
    /// <returns>The filtered contents, in the order they came in.</returns>
    public static async Task<IReadOnlyList<WebPageModelContent>> SanitizeAsync(PromptInjectionGuardService guardService,
        IReadOnlyList<(WebPageModelContent Content, PromptInjectionSource Source)> pages)
    {
        if (pages.Count is 0)
            return [];

        List<PromptInjectionText> texts = [];
        foreach (var (content, source) in pages)
        {
            texts.Add(new(content.Markdown, source));
            texts.Add(new(content.Title, source));
            texts.Add(new(content.Description, source));
            texts.Add(new(content.Language, source));
            texts.Add(new(content.PublishedTime, source));
            texts.Add(new(content.ModifiedTime, source));

            foreach (var author in content.Authors)
                texts.Add(new(author, source));
        }

        var sanitizedTexts = await guardService.SanitizeAsync(texts);
        var sanitizedPages = new List<WebPageModelContent>(pages.Count);
        var offset = 0;
        
        foreach (var (content, _) in pages)
        {
            var authors = new List<string>(content.Authors.Count);
            for (var authorIndex = 0; authorIndex < content.Authors.Count; authorIndex++)
                authors.Add(sanitizedTexts[offset + SINGLE_VALUE_FIELD_COUNT + authorIndex]);

            sanitizedPages.Add(new(
                sanitizedTexts[offset],
                sanitizedTexts[offset + 1],
                sanitizedTexts[offset + 2],
                authors,
                sanitizedTexts[offset + 3],
                sanitizedTexts[offset + 4],
                sanitizedTexts[offset + 5]));

            offset += SINGLE_VALUE_FIELD_COUNT + content.Authors.Count;
        }

        return sanitizedPages;
    }

    /// <summary>
    /// Filters every model-facing text of a single page.
    /// </summary>
    public static async Task<WebPageModelContent> SanitizeAsync(
        PromptInjectionGuardService guardService,
        WebPageModelContent content,
        PromptInjectionSource source) => (await SanitizeAsync(guardService, [(content, source)]))[0];
}