namespace AIStudio.Tools;

/// <summary>
/// Reads the JSON part out of a model answer.
/// </summary>
/// <remarks>
/// Models like to wrap the JSON they were asked for in prose or in a code fence, however strictly a
/// prompt asks for JSON alone. This class only finds where the JSON starts and where it ends, and it
/// never checks the JSON itself: broken JSON reaches the deserializer of the caller as the model wrote
/// it, so that the message of the deserializer can tell the model where it breaks.
/// </remarks>
public static class ModelAnswerJson
{
    /// <summary>
    /// What may follow the opening brace of an object, apart from whitespace: the quote of its first
    /// name, the closing brace of an empty object, or a comment, which some callers allow.
    /// </summary>
    private const string OBJECT_CONTENT_STARTS = "\"}/";

    /// <summary>
    /// Reads the first JSON object out of a model answer.
    /// </summary>
    /// <remarks>
    /// Braces in prose, such as a placeholder like {name}, are skipped, because an object starts with
    /// a brace followed by a quote, a closing brace, or a comment. Only when the answer holds no such
    /// brace, the first brace at all counts, so that broken JSON still reaches the deserializer.
    /// </remarks>
    /// <param name="answer">The answer of the model.</param>
    /// <returns>The first complete JSON object, or an empty span when there is none, e.g., because the answer breaks off.</returns>
    public static ReadOnlySpan<char> ExtractObject(ReadOnlySpan<char> answer) => Extract(answer, '{', '}', OBJECT_CONTENT_STARTS);

    private static ReadOnlySpan<char> Extract(ReadOnlySpan<char> answer, char opening, char closing, ReadOnlySpan<char> contentStarts)
    {
        var start = FindStart(answer, opening, contentStarts);
        if (start is null)
            return [];

        var end = FindEnd(answer, start.Value, opening, closing);
        return end is null ? [] : answer[start.Value..(end.Value + 1)];
    }

    /// <summary>
    /// Finds the first opening bracket which is followed by what may start the JSON content, or else
    /// the first opening bracket at all.
    /// </summary>
    private static int? FindStart(ReadOnlySpan<char> answer, char opening, ReadOnlySpan<char> contentStarts)
    {
        int? firstOpening = null;
        for (var index = 0; index < answer.Length; index++)
        {
            if (answer[index] != opening)
                continue;

            firstOpening ??= index;
            var content = answer[(index + 1)..].TrimStart();
            if (!content.IsEmpty && contentStarts.Contains(content[0]))
                return index;
        }

        return firstOpening;
    }

    /// <summary>
    /// Finds the bracket which closes the one at the start. Only brackets of the same kind count,
    /// and only outside of strings.
    /// </summary>
    private static int? FindEnd(ReadOnlySpan<char> answer, int start, char opening, char closing)
    {
        var depth = 0;
        var insideString = false;
        for (var index = start; index < answer.Length; index++)
        {
            if (answer[index] == '"' && !IsEscaped(answer, index))
                insideString = !insideString;

            if (insideString)
                continue;

            if (answer[index] == opening)
                depth++;
            else if (answer[index] == closing && --depth == 0)
                return index;
        }

        return null;
    }

    private static bool IsEscaped(ReadOnlySpan<char> answer, int index)
    {
        var backslashCount = 0;
        for (var i = index - 1; i >= 0 && answer[i] == '\\'; i--)
            backslashCount++;

        return backslashCount % 2 == 1;
    }
}