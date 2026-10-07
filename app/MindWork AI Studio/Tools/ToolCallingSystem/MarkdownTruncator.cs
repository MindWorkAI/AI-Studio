namespace AIStudio.Tools.ToolCallingSystem;

internal static class MarkdownTruncator
{
    public static string Truncate(string markdown, int maxCharacters)
    {
        const string TRUNCATION_MARKER = "[Page content truncated]";
        if (maxCharacters <= TRUNCATION_MARKER.Length)
            return markdown[..maxCharacters];

        var contentLimit = maxCharacters - TRUNCATION_MARKER.Length - 2;
        var breakPosition = markdown.LastIndexOf("\n\n", contentLimit, StringComparison.Ordinal);
        if (breakPosition < contentLimit / 2)
            breakPosition = markdown.LastIndexOf('\n', contentLimit);
        if (breakPosition < contentLimit / 2)
            breakPosition = contentLimit;

        return $"{markdown[..breakPosition].TrimEnd()}\n\n{TRUNCATION_MARKER}";
    }
}
