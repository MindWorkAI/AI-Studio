namespace AIStudio.Chat;

public static class StringExtensions
{
    public static string RemoveThinkTags(this string input)
    {
        const string OPEN_TAG = "<think>";
        const string CLOSE_TAG = "</think>";
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith(OPEN_TAG, StringComparison.Ordinal))
            return input;
        
        var endIndex = input.IndexOf(CLOSE_TAG, StringComparison.Ordinal);
        if (endIndex == -1)
            return string.Empty;
        
        return input[(endIndex + CLOSE_TAG.Length)..];
    }

    /// <summary>
    /// Shortens a text to the given number of characters, and marks the cut with three dots.
    /// </summary>
    /// <remarks>
    /// Never cuts between the two halves of a surrogate pair, which no JSON writer takes.<br/><br/>
    /// Takes and returns a string rather than a span on purpose: most texts are short enough, and
    /// those come back as the very same instance. Only a text which is cut costs one new string.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="maxCharacters">How many characters of the text to keep at most, the dots not counted.</param>
    /// <returns>The text as it was when it is short enough, otherwise its beginning followed by three dots.</returns>
    public static string Shorten(this string text, int maxCharacters)
    {
        if (text.Length <= maxCharacters)
            return text;

        var end = char.IsHighSurrogate(text[maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters;
        return string.Concat(text.AsSpan(0, end).TrimEnd(), "...");
    }
}