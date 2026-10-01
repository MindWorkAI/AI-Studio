namespace AIStudio.Tools.Web;

/// <summary>
/// Decides from a response's media type whether AI Studio can read it, and how.
/// </summary>
internal static class WebContentTypeClassifier
{
    private static readonly HashSet<string> HTML_MEDIA_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html", "application/xhtml+xml",
    };

    /// <summary>
    /// The text formats outside of text/* which are worth reading. JSON and XML are covered by
    /// their suffixes as well, so problem+json or rss+xml need no entry of their own.
    /// </summary>
    private static readonly HashSet<string> APPLICATION_TEXT_MEDIA_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json", "application/xml", "application/x-ndjson", "application/javascript", "application/x-javascript",
        "application/yaml", "application/x-yaml", "application/toml", "application/sql",
    };

    /// <summary>
    /// Classifies a media type, such as text/html or application/json.
    /// </summary>
    /// <remarks>
    /// A missing media type counts as HTML, as it always did: servers leaving it out are
    /// almost always serving a page.<br/><br/>
    /// Binary formats are not readable, even those holding text, such as PDF: their bytes
    /// decoded as text are noise, and what a model would make of them is worse than nothing.
    /// The suffix rules apply to application/* only, which keeps image/svg+xml out.
    /// </remarks>
    /// <param name="mediaType">The media type without its parameters.</param>
    /// <returns>How the content is read, or null when it cannot be read.</returns>
    public static WebContentKind? Classify(string mediaType)
    {
        mediaType = mediaType.Trim();
        if (mediaType.Length is 0 || HTML_MEDIA_TYPES.Contains(mediaType))
            return WebContentKind.HTML_PAGE;

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || APPLICATION_TEXT_MEDIA_TYPES.Contains(mediaType) || HasTextSuffix(mediaType))
            return WebContentKind.TEXT_DOCUMENT;

        return null;
    }

    /// <summary>
    /// Whether an application/* type states that it is JSON or XML, such as problem+json.
    /// </summary>
    private static bool HasTextSuffix(string mediaType) =>
        mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase) &&
        (mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));
}