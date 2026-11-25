namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Reads the text of a result from the answer of the model.
/// </summary>
/// <remarks>
/// The request asks for plain Markdown, but some models wrap a document in a code block all the same. Such a block
/// would show the result as code and export it as code, so it is taken off; everything else stays as the model wrote
/// it, for the person to read and edit.
/// </remarks>
public static class BflResultText
{
    /// <summary>
    /// Reads the text of a result from the answer of the model.
    /// </summary>
    /// <param name="answer">The answer of the model.</param>
    /// <returns>The text, without a code block around all of it; empty when the model wrote nothing.</returns>
    public static string Read(string answer)
    {
        var text = answer.ReplaceLineEndings("\n").Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal) && !text.StartsWith("~~~", StringComparison.Ordinal))
            return text;

        var fence = text[..3];
        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0 || !text.EndsWith(fence, StringComparison.Ordinal) || text.Length - fence.Length <= firstLineEnd)
            return text;

        var inner = text[(firstLineEnd + 1)..^fence.Length];

        // A fence inside means the block is one of several, not a wrapper around the whole answer:
        return inner.Contains(fence, StringComparison.Ordinal) ? text : inner.Trim();
    }
}