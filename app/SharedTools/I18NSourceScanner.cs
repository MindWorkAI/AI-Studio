namespace SharedTools;

/// <summary>
/// Finds the texts of T() and TB() in a C# or Razor source file.
/// </summary>
/// <remarks>
/// A text counts when its literal follows the opening parenthesis right away and is the whole first argument.
/// Every kind of literal works, also a raw literal over several lines; see CSharpStringLiteral.
/// </remarks>
public static class I18NSourceScanner
{
    private static readonly string[] START_TAGS = ["TB(", "T("];

    /// <summary>
    /// Finds the texts in the content of a source file.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="filePath">The path of the file, for the warnings.</param>
    /// <param name="warnings">Collects a warning for each call whose text cannot be translated.</param>
    /// <returns>The texts, in the order of the file.</returns>
    public static IReadOnlyList<I18NSourceText> FindTexts(string content, string filePath, ICollection<string> warnings)
    {
        var texts = new List<I18NSourceText>();
        var position = 0;
        while (TryFindNextCall(content, position, out var literalStart))
        {
            position = literalStart;
            var rest = content.AsSpan(literalStart);
            if (rest.StartsWith("$") || rest.StartsWith("@$"))
            {
                warnings.Add($"- Warning: An interpolated text in the file '{filePath}' at position {literalStart} cannot be translated, since its text is known only at runtime.");
                continue;
            }

            if (!rest.StartsWith("\"") && !rest.StartsWith("@\""))
                continue;

            if (!CSharpStringLiteral.TryRead(rest, out var text, out var length))
            {
                warnings.Add($"- Warning: The text in the file '{filePath}' at position {literalStart} is no complete string literal.");
                continue;
            }

            position = literalStart + length;
            var next = rest[length..].TrimStart();
            if (next.IsEmpty || next[0] is not (')' or ','))
            {
                warnings.Add($"- Warning: The text '{text}' in the file '{filePath}' is not the whole first argument, e.g., because it is concatenated. Its key does not match the key the app looks up at runtime.");
                continue;
            }

            texts.Add(new I18NSourceText(literalStart, length, text));
        }

        return texts;
    }

    /// <summary>
    /// Finds the next call of T() or TB() at or after the position, i.e., a start tag which no identifier precedes.
    /// </summary>
    private static bool TryFindNextCall(string content, int position, out int argumentStart)
    {
        argumentStart = -1;
        while (position < content.Length)
        {
            var bestIndex = -1;
            var bestLength = 0;
            foreach (var tag in START_TAGS)
            {
                var index = content.IndexOf(tag, position, StringComparison.Ordinal);
                if (index >= 0 && (bestIndex < 0 || index < bestIndex))
                {
                    bestIndex = index;
                    bestLength = tag.Length;
                }
            }

            if (bestIndex < 0)
                return false;

            if (bestIndex > 0 && IsIdentifierPart(content[bestIndex - 1]))
            {
                position = bestIndex + 1;
                continue;
            }

            argumentStart = bestIndex + bestLength;
            return true;
        }

        return false;
    }

    private static bool IsIdentifierPart(char character) => char.IsLetterOrDigit(character) || character == '_';
}