namespace AIStudio.Chat;

public static class StringExtensions
{
    /// <summary>
    /// The markers with which open models write their thinking into the answer itself, as pairs of
    /// the opening and the closing marker.
    /// </summary>
    /// <remarks>
    /// DeepSeek, Qwen, GLM, Kimi K2, Nemotron, and MiniMax write &lt;think&gt;, Magistral [THINK],
    /// Command A of Cohere its own markers, older versions of Kimi ◁think▷, and Gemma 4 a channel
    /// named thought. A server which separates the thinking, e.g., into reasoning_content, leaves
    /// none of them in the answer.
    /// </remarks>
    private static readonly (string Open, string Close)[] THINKING_MARKERS =
    [
        ("<think>", "</think>"),
        ("[THINK]", "[/THINK]"),
        ("<|START_THINKING|>", "<|END_THINKING|>"),
        ("◁think▷", "◁/think▷"),
        ("<|channel>thought", "<channel|>"),
    ];

    /// <summary>
    /// Where gpt-oss starts its thinking in the Harmony format, when the server leaves the channels in the answer.
    /// </summary>
    private const string HARMONY_ANALYSIS = "<|channel|>analysis";

    /// <summary>
    /// How a message of the model starts in the Harmony format, when the server leaves it in the answer.
    /// </summary>
    private const string HARMONY_START = "<|start|>assistant";

    /// <summary>
    /// Where gpt-oss starts its answer in the Harmony format.
    /// </summary>
    private const string HARMONY_FINAL = "<|channel|>final<|message|>";

    private static readonly string[] HARMONY_END_TOKENS = ["<|return|>", "<|end|>"];

    /// <param name="text">The text.</param>
    extension(string text)
    {
        /// <summary>
        /// Removes the thinking which a model wrote into its answer, so that only the answer remains.
        /// </summary>
        /// <remarks>
        /// Recognizes the markers of THINKING_MARKERS and the channels of the Harmony format:
        /// <list type="bullet">
        /// <item>One or several blocks of thinking at the beginning are removed, also after a blank line.</item>
        /// <item>A block which is not closed yet means that the model is still thinking, so nothing remains.</item>
        /// <item>A closing marker without its opening one ends the thinking as well: since Qwen 3.5, the opening
        /// &lt;think&gt; is part of the prompt, and only the closing one appears in the answer. It counts only when
        /// no opening marker of its kind stands before it, so that an answer which explains these markers stays whole.</item>
        /// </list>
        /// Thinking in the middle of an answer stays, as does thinking in plain text, such as that of Granite 3.2:
        /// neither can be told apart from an answer which merely mentions it.
        /// </remarks>
        /// <returns>The answer without the thinking; the text as it was when there is none.</returns>
        public string RemoveThinkTags()
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var finalChannel = text.LastIndexOf(HARMONY_FINAL, StringComparison.Ordinal);
            if (finalChannel >= 0)
                return WithoutHarmonyEndTokens(text[(finalChannel + HARMONY_FINAL.Length)..]);

            //
            // gpt-oss is still thinking, since its answer has not started yet:
            //
            var rest = text.AsSpan().TrimStart();
            if (rest.StartsWith(HARMONY_ANALYSIS, StringComparison.Ordinal) || rest.StartsWith(HARMONY_START + HARMONY_ANALYSIS, StringComparison.Ordinal))
                return string.Empty;

            var removedBlock = false;
            while (ClosingMarkerOfLeadingBlock(rest) is { } close)
            {
                var end = rest.IndexOf(close, StringComparison.Ordinal);
                if (end < 0)
                    return string.Empty;

                rest = rest[(end + close.Length)..].TrimStart();
                removedBlock = true;
            }

            if (removedBlock)
                return rest.ToString();

            foreach (var (open, close) in THINKING_MARKERS)
            {
                var end = text.IndexOf(close, StringComparison.Ordinal);
                if (end < 0 || text.AsSpan(0, end).Contains(open, StringComparison.Ordinal))
                    continue;

                return text[(end + close.Length)..].TrimStart();
            }

            return text;
        }

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
    /// The closing marker of the block of thinking the text starts with, or null when it starts with none.
    /// </summary>
    private static string? ClosingMarkerOfLeadingBlock(ReadOnlySpan<char> text)
    {
        foreach (var (open, close) in THINKING_MARKERS)
            if (text.StartsWith(open, StringComparison.Ordinal))
                return close;

        return null;
    }

    /// <summary>
    /// Removes the tokens with which the Harmony format ends a message, which a server may leave in the answer.
    /// </summary>
    private static string WithoutHarmonyEndTokens(string answer)
    {
        var trimmed = answer.TrimEnd();
        var removedToken = true;
        while (removedToken)
        {
            removedToken = false;
            foreach (var token in HARMONY_END_TOKENS)
            {
                if (!trimmed.EndsWith(token, StringComparison.Ordinal))
                    continue;

                trimmed = trimmed[..^token.Length].TrimEnd();
                removedToken = true;
            }
        }

        return trimmed.TrimStart();
    }
}