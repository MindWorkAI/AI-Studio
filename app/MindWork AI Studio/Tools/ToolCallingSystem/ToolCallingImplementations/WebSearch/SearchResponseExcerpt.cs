namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// Quotes part of a search service's response in an error message.
/// </summary>
/// <remarks>
/// A failed search says what the service answered, because a status code alone rarely explains
/// itself. That answer goes into a log line and into the tool result, so it has to stay on one
/// line and stay short: an HTML error page would otherwise push the actual message out of
/// sight.
/// </remarks>
internal static class SearchResponseExcerpt
{
    private const int MAX_EXCERPT_LENGTH = 400;

    public static string Create(string responseBody)
    {
        var sanitizedResponseBody = string.Concat(responseBody.Select(character => char.IsControl(character) ? ' ' : character));
        var excerpt = string.Join(" ", sanitizedResponseBody
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return excerpt[..Math.Min(excerpt.Length, MAX_EXCERPT_LENGTH)];
    }

    /// <summary>
    /// The excerpt as a sentence appended to an error message, or nothing when there is no body.
    /// </summary>
    public static string CreateDetails(string responseBody)
    {
        var excerpt = Create(responseBody);
        return string.IsNullOrWhiteSpace(excerpt) ? string.Empty : $" Response body: {excerpt}";
    }
}