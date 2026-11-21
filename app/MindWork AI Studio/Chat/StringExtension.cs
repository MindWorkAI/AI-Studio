namespace AIStudio.Chat;

public static class StringExtensions
{
    private const string THINK_OPEN_TAG = "<think>";
    private const string THINK_CLOSE_TAG = "</think>";

    /// <param name="text">The text.</param>
    extension(string text)
    {
        /// <summary>
        /// The text without the think block which a model may open its answer with.
        /// </summary>
        /// <returns>The text as it was without such a block, nothing while the block is still open, and otherwise what follows the block.</returns>
        public string RemoveThinkTags() => AnswerStart(text) is { } start ? text[start..] : string.Empty;

        /// <summary>
        /// Whether the text holds anything to read besides the think block which a model may open its answer with.
        /// </summary>
        /// <remarks>
        /// Tells what RemoveThinkTags would leave without copying it: the chat asks with every chunk of an answer,
        /// and a model which thinks out loud writes long texts.
        /// </remarks>
        public bool HasTextBeyondThinkTags => AnswerStart(text) is { } start && !text.AsSpan(start).IsWhiteSpace();

        /// <summary>
        /// Shortens a text to the given number of characters, and marks the cut with three dots.
        /// </summary>
        /// <remarks>
        /// Never cuts between the two halves of a surrogate pair, which no JSON writer takes.<br/><br/>
        /// Takes and returns a string rather than a span on purpose: most texts are short enough, and
        /// those come back as the very same instance. Only a text which is cut costs one new string.
        /// </remarks>
        /// <param name="maxCharacters">How many characters of the text to keep at most, the dots not counted.</param>
        /// <returns>The text as it was when it is short enough, otherwise its beginning followed by three dots.</returns>
        public string Shorten(int maxCharacters)
        {
            if (text.Length <= maxCharacters)
                return text;

            var end = char.IsHighSurrogate(text[maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters;
            return string.Concat(text.AsSpan(0, end).TrimEnd(), "...");
        }
    }

    /// <summary>
    /// Where the answer starts in a text which a model may open with a think block.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>0 without a think block, the position right behind the block otherwise, and null while the block is still open.</returns>
    private static int? AnswerStart(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(THINK_OPEN_TAG, StringComparison.Ordinal))
            return 0;

        var endIndex = text.IndexOf(THINK_CLOSE_TAG, StringComparison.Ordinal);
        return endIndex == -1 ? null : endIndex + THINK_CLOSE_TAG.Length;
    }
}