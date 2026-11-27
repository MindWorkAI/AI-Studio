using AIStudio.Models;

namespace AIStudio.AIOperations.ConversationSummary;

/// <summary>
/// How much room a summary of a conversation needs and may take.
/// </summary>
/// <remarks>
/// Pure functions, so that the summary itself and the notice before sending, which decides whether compressing
/// would help, count with the same numbers.
/// </remarks>
public static class ConversationSummaryBudget
{
    /// <summary>
    /// The least room kept free for the summary the AI writes.
    /// </summary>
    public const int MIN_TOKENS_FOR_THE_SUMMARY = 1_024;

    /// <summary>
    /// The smallest context window a model needs for writing a summary.
    /// </summary>
    /// <remarks>
    /// The system prompt, the room for the summary, and the summary so far take about 4,600 tokens
    /// before a single message fits. Close to that, every part holds only a few messages, and the
    /// summary is written anew for each of them. From 8,192 tokens on, a part holds a few
    /// thousand tokens of the conversation.
    /// </remarks>
    public const int MIN_CONTEXT_WINDOW = 8_192;

    /// <summary>
    /// How much longer than the room kept for it a summary may grow before the AI shortens it, see IsTooLong.
    /// </summary>
    /// <remarks>
    /// Models cannot count what they write. The request already asks for half as many words as the
    /// room holds tokens, which leaves a margin of its own; beyond half as much again, the summary
    /// takes room the next part or the new chat needs.
    /// </remarks>
    internal const double SUMMARY_TOLERANCE = 1.5;

    /// <summary>
    /// Whether a model with the given context window can write a summary.
    /// </summary>
    /// <remarks>
    /// An unknown window does not stop it: the whole conversation then goes into one request, and
    /// the provider tells when it is too long, see ConversationSummaryOperation.RunAsync.
    /// </remarks>
    /// <param name="window">The context window of the model which writes the summary.</param>
    /// <returns>True when the window is unknown or at least MIN_CONTEXT_WINDOW.</returns>
    public static bool CanSummarizeWith(ContextWindow window) => !window.IsKnown || window.DefaultTokens >= MIN_CONTEXT_WINDOW;

    /// <summary>
    /// How many tokens the summary should take in a model with the given context window.
    /// </summary>
    /// <remarks>
    /// A quarter of the window, at least MIN_TOKENS_FOR_THE_SUMMARY and at most what the settings allow, see
    /// DataChatCompression.MaxSummaryTokens.
    /// It is a target, not a limit: the AI is asked for a summary of this length, and one which grows
    /// far beyond it is shortened once, see ConversationSummaryOperation.RunAsync.
    /// </remarks>
    /// <param name="windowTokens">The context window of the model which writes the summary.</param>
    /// <param name="maxSummaryTokens">The most tokens a summary should take; never fewer than MIN_TOKENS_FOR_THE_SUMMARY.</param>
    /// <returns>The tokens kept free for the summary.</returns>
    public static int TokensForTheSummary(int windowTokens, int maxSummaryTokens) => Math.Clamp(windowTokens / 4, MIN_TOKENS_FOR_THE_SUMMARY, Math.Max(MIN_TOKENS_FOR_THE_SUMMARY, maxSummaryTokens));

    /// <summary>
    /// How many tokens the summary should take when one model writes it and another one continues with it.
    /// </summary>
    /// <remarks>
    /// The summary has to fit into both windows: the model which writes it holds it next to the next part of the
    /// conversation, and the model of the chat holds it next to the last exchange. So the smaller room wins.
    /// </remarks>
    /// <param name="writerWindow">The context window of the model which writes the summary.</param>
    /// <param name="targetWindow">The context window of the model which continues with the summary, e.g., the model of the chat.</param>
    /// <param name="maxSummaryTokens">The most tokens a summary should take.</param>
    /// <returns>The tokens kept free for the summary, or 0 when neither window is known.</returns>
    public static int TokensForTheSummary(ContextWindow writerWindow, ContextWindow targetWindow, int maxSummaryTokens)
    {
        if (!writerWindow.IsKnown)
            return targetWindow.IsKnown ? TokensForTheSummary(targetWindow.DefaultTokens, maxSummaryTokens) : 0;

        var forTheWriter = TokensForTheSummary(writerWindow.DefaultTokens, maxSummaryTokens);
        return targetWindow.IsKnown ? Math.Min(forTheWriter, TokensForTheSummary(targetWindow.DefaultTokens, maxSummaryTokens)) : forTheWriter;
    }

    /// <summary>
    /// Whether a summary grew so long beyond the room kept for it that the AI has to shorten it.
    /// </summary>
    /// <param name="summaryTokens">The tokens the summary takes.</param>
    /// <param name="tokensForTheSummary">The room kept for the summary, or 0 when the window of the model is unknown.</param>
    /// <returns>True when the summary takes more than SUMMARY_TOLERANCE times the room kept for it; never without a room.</returns>
    public static bool IsTooLong(int summaryTokens, int tokensForTheSummary) => tokensForTheSummary > 0 && summaryTokens > tokensForTheSummary * SUMMARY_TOLERANCE;
}