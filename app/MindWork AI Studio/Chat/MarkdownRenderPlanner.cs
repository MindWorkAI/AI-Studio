namespace AIStudio.Chat;

/// <summary>
/// Cuts the text of a message into the parts the chat renders one by one: Markdown, and the formulas
/// between $$ or \[ and \] on lines of their own, or in a code block of latex, tex, or math which holds
/// nothing but a formula, see LatexFormula.
/// </summary>
/// <remarks>
/// MudMarkdown shows a formula as it is, and MathJax typesets only what the chat hands it as a
/// formula block. A formula therefore has to leave the Markdown around it. Inside a code block, $$ is
/// code, not a formula, and a formula block the model never closed leaves the whole text as Markdown.
/// </remarks>
internal static class MarkdownRenderPlanner
{
    private const string HTML_START_TAG = "<";
    private const string HTML_END_TAG = "</";
    private const string HTML_SELF_CLOSING_TAG = "/>";
    private const string CODE_FENCE_MARKER_BACKTICK = "```";
    private const string CODE_FENCE_MARKER_TILDE = "~~~";
    private const string MATH_BLOCK_MARKER_DOLLAR = "$$";
    private const string MATH_BLOCK_MARKER_BRACKET_OPEN = """\[""";
    private const string MATH_BLOCK_MARKER_BRACKET_CLOSE = """\]""";
    private const string HTML_CODE_FENCE_PREFIX = "```html";

    private static readonly string[] HTML_TAG_MARKERS =
    [
        "<!doctype",
        "<html",
        "<head",
        "<body",
        "<style",
        "<script",
        "<iframe",
        "<svg",
    ];

    /// <summary>
    /// Cuts a text into the parts the chat renders.
    /// </summary>
    /// <param name="text">The text of a message, as stored.</param>
    /// <returns>The plan, or MarkdownRenderPlan.EMPTY when the text shows nothing.</returns>
    public static MarkdownRenderPlan Build(string text)
    {
        var normalized = NormalizeMarkdownForRendering(text);
        if (string.IsNullOrWhiteSpace(normalized))
            return MarkdownRenderPlan.EMPTY;

        var normalizedSpan = normalized.AsSpan();
        var segments = new List<MarkdownRenderSegment>();
        var activeCodeFenceMarker = '\0';
        var activeMathBlockFenceType = MathBlockFenceType.NONE;
        var markdownSegmentStart = 0;
        var mathContentStart = 0;
        var mathBlockStart = 0;
        var codeFenceStart = 0;
        var codeContentStart = 0;
        var codeFenceLanguage = ReadOnlySpan<char>.Empty;

        for (var lineStart = 0; lineStart < normalizedSpan.Length;)
        {
            var lineEnd = lineStart;
            while (lineEnd < normalizedSpan.Length && normalizedSpan[lineEnd] is not '\r' and not '\n')
                lineEnd++;

            var nextLineStart = lineEnd;
            if (nextLineStart < normalizedSpan.Length)
            {
                if (normalizedSpan[nextLineStart] == '\r')
                    nextLineStart++;

                if (nextLineStart < normalizedSpan.Length && normalizedSpan[nextLineStart] == '\n')
                    nextLineStart++;
            }

            var trimmedLine = TrimWhitespace(normalizedSpan[lineStart..lineEnd]);
            var wasInCodeBlock = activeCodeFenceMarker != '\0';
            if (activeMathBlockFenceType is MathBlockFenceType.NONE && TryUpdateCodeFenceState(trimmedLine, ref activeCodeFenceMarker))
            {
                if (!wasInCodeBlock)
                {
                    codeFenceStart = lineStart;
                    codeContentStart = nextLineStart;
                    codeFenceLanguage = FenceLanguageOf(trimmedLine);
                }
                else if (activeCodeFenceMarker == '\0' && LatexFormula.TryFind(codeFenceLanguage, normalizedSpan[codeContentStart..lineStart], out var formula))
                {
                    var (formulaStart, formulaLength) = formula.GetOffsetAndLength(lineStart - codeContentStart);
                    AddMarkdownSegment(markdownSegmentStart, codeFenceStart);
                    segments.Add(new(MarkdownRenderSegmentType.MATH_BLOCK, codeContentStart + formulaStart, formulaLength, codeFenceStart));
                    markdownSegmentStart = nextLineStart;
                }

                lineStart = nextLineStart;
                continue;
            }

            if (activeCodeFenceMarker != '\0')
            {
                lineStart = nextLineStart;
                continue;
            }

            if (activeMathBlockFenceType is MathBlockFenceType.NONE)
            {
                if (trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_DOLLAR.AsSpan()))
                {
                    AddMarkdownSegment(markdownSegmentStart, lineStart);
                    mathBlockStart = lineStart;
                    mathContentStart = nextLineStart;
                    activeMathBlockFenceType = MathBlockFenceType.DOLLAR;
                    lineStart = nextLineStart;
                    continue;
                }

                if (trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_BRACKET_OPEN.AsSpan()))
                {
                    AddMarkdownSegment(markdownSegmentStart, lineStart);
                    mathBlockStart = lineStart;
                    mathContentStart = nextLineStart;
                    activeMathBlockFenceType = MathBlockFenceType.BRACKET;
                }
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.DOLLAR && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_DOLLAR.AsSpan()))
            {
                segments.Add(MathSegment(normalizedSpan, mathContentStart, lineStart, mathBlockStart));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
            }
            else if (activeMathBlockFenceType is MathBlockFenceType.BRACKET && trimmedLine.SequenceEqual(MATH_BLOCK_MARKER_BRACKET_CLOSE.AsSpan()))
            {
                segments.Add(MathSegment(normalizedSpan, mathContentStart, lineStart, mathBlockStart));

                markdownSegmentStart = nextLineStart;
                activeMathBlockFenceType = MathBlockFenceType.NONE;
            }

            lineStart = nextLineStart;
        }

        if (activeMathBlockFenceType is not MathBlockFenceType.NONE)
            return new(normalized, [new(MarkdownRenderSegmentType.MARKDOWN, 0, normalized.Length, 0)]);

        AddMarkdownSegment(markdownSegmentStart, normalized.Length);
        if (segments.Count == 0)
            segments.Add(new(MarkdownRenderSegmentType.MARKDOWN, 0, normalized.Length, 0));

        return new(normalized, segments);

        void AddMarkdownSegment(int start, int end)
        {
            if (end <= start)
                return;

            segments.Add(new(MarkdownRenderSegmentType.MARKDOWN, start, end - start, start));
        }
    }

    private static string NormalizeMarkdownForRendering(string text)
    {
        var textWithoutThinkTags = text.RemoveThinkTags();
        var trimmed = TrimWhitespace(textWithoutThinkTags.AsSpan());
        if (trimmed.IsEmpty)
            return string.Empty;

        var cleaned = trimmed.Length == textWithoutThinkTags.Length
            ? textWithoutThinkTags
            : trimmed.ToString();

        if (cleaned.Contains(CODE_FENCE_MARKER_BACKTICK, StringComparison.Ordinal))
            return cleaned;

        if (LooksLikeRawHtml(cleaned))
            return $"{HTML_CODE_FENCE_PREFIX}\n{cleaned}\n{CODE_FENCE_MARKER_BACKTICK}";

        return cleaned;
    }

    private static bool LooksLikeRawHtml(string text)
    {
        var content = text.AsSpan();
        var start = 0;
        while (start < content.Length && char.IsWhiteSpace(content[start]))
            start++;

        content = content[start..];
        if (!content.StartsWith(HTML_START_TAG.AsSpan(), StringComparison.Ordinal))
            return false;

        foreach (var marker in HTML_TAG_MARKERS)
            if (content.IndexOf(marker.AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

        return content.IndexOf(HTML_END_TAG.AsSpan(), StringComparison.Ordinal) >= 0
               || content.IndexOf(HTML_SELF_CLOSING_TAG.AsSpan(), StringComparison.Ordinal) >= 0;
    }

    private static bool TryUpdateCodeFenceState(ReadOnlySpan<char> trimmedLine, ref char activeCodeFenceMarker)
    {
        var fenceMarker = '\0';
        if (trimmedLine.StartsWith(CODE_FENCE_MARKER_BACKTICK.AsSpan(), StringComparison.Ordinal))
            fenceMarker = '`';
        else if (trimmedLine.StartsWith(CODE_FENCE_MARKER_TILDE.AsSpan(), StringComparison.Ordinal))
            fenceMarker = '~';

        if (fenceMarker == '\0')
            return false;

        activeCodeFenceMarker = activeCodeFenceMarker == '\0'
            ? fenceMarker
            : activeCodeFenceMarker == fenceMarker
                ? '\0'
                : activeCodeFenceMarker;

        return true;
    }

    private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> text)
    {
        var start = 0;
        var end = text.Length - 1;

        while (start < text.Length && char.IsWhiteSpace(text[start]))
            start++;

        while (end >= start && char.IsWhiteSpace(text[end]))
            end--;

        return start > end ? ReadOnlySpan<char>.Empty : text[start..(end + 1)];
    }

    /// <summary>
    /// Reads the language behind an opening fence, such as latex in ```latex.
    /// </summary>
    private static ReadOnlySpan<char> FenceLanguageOf(ReadOnlySpan<char> fence)
    {
        var info = TrimWhitespace(fence.TrimStart(fence[0]));
        var end = 0;
        while (end < info.Length && !char.IsWhiteSpace(info[end]))
            end++;

        return info[..end];
    }

    /// <summary>
    /// The part of a formula block between its markers, without the line breaks around it.
    /// </summary>
    private static MarkdownRenderSegment MathSegment(ReadOnlySpan<char> text, int start, int end, int blockStart)
    {
        while (start < end && text[start] is '\r' or '\n')
            start++;

        while (end > start && text[end - 1] is '\r' or '\n')
            end--;

        return new(MarkdownRenderSegmentType.MATH_BLOCK, start, end - start, blockStart);
    }

    private enum MathBlockFenceType
    {
        NONE,
        DOLLAR,
        BRACKET,
    }
}