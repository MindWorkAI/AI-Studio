using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Models;
using AIStudio.Tools.Services;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Decides whether compressing a chat would make enough room in the context window of its model.
/// </summary>
/// <remarks>
/// The summary itself is written by an AI operation, see ConversationSummaryOperation. This service holds what
/// belongs to the chat: whether the notice before sending offers to compress it.
/// </remarks>
/// <param name="tokenCounter">Counts the tokens of the messages, with the tokenizer of the provider.</param>
public sealed class ChatCompressionService(ConversationTokenCounter tokenCounter)
{
    /// <summary>
    /// Decides whether the notice before sending offers to compress a chat, or why compressing would not help.
    /// </summary>
    /// <remarks>
    /// Compressing replaces only the messages the plan summarizes, see ChatCompressionPlan.Of: the system prompt, the
    /// example conversation of the chat template, the last message of the user with its files, and the last answer of
    /// the AI stay word for word, and the message about to be sent goes along unchanged. So it helps only when what is
    /// left, together with the room for the summary, stays below the share from which AI Studio asks before sending,
    /// see ConversationTokens.SHARE_TO_ASK_BEFORE_SENDING. The checks which need no count come first, see
    /// OfferBeforeCounting. Then the summarized messages are counted with their files, which the count of the whole
    /// conversation mostly left in the cache of the counter already, and taken off that count, see KeepsTooMuch.
    /// When they cannot be counted, compressing is offered, since nothing speaks against it.<br/><br/>
    ///
    /// Whether the place which asks allows compressing at all, e.g., with workspaces switched off, is left to that
    /// place: it asks only where it does.
    /// </remarks>
    /// <param name="chat">The chat to compress.</param>
    /// <param name="exampleConversationLength">How many blocks the example conversation of the chat template holds, 0 without a template.</param>
    /// <param name="provider">The provider which would write the summary, with the tokenizer to count with.</param>
    /// <param name="tokensWithMessage">What the chat costs together with the message about to be sent, with the context window of the model.</param>
    /// <param name="token">Ends the count when nobody needs the answer anymore.</param>
    /// <returns>Whether compressing is offered, or why not.</returns>
    public async Task<ChatCompressionOffer> OfferBeforeSendingAsync(ChatThread chat, int exampleConversationLength, AIStudio.Settings.Provider provider, ConversationTokens tokensWithMessage, CancellationToken token = default)
    {
        // The message is in the input field, so an unanswered last message is summarized rather than taken along:
        var plan = ChatCompressionPlan.Of(chat, exampleConversationLength, draftIsEmpty: false);
        var offer = OfferBeforeCounting(tokensWithMessage.Window, plan is not null, tokensWithMessage.DraftTokens);
        if (offer is not ChatCompressionOffer.OFFERED || plan is null)
            return offer;

        var summarizedParts = ConversationParts.Of(new ChatThread { Blocks = [.. plan.ToSummarize] }, string.Empty, string.Empty, null, false, null);
        var summarized = await tokenCounter.CountAsync(provider, summarizedParts, ReportedHistory.UNKNOWN, token);
        if (!summarized.IsKnown)
            return ChatCompressionOffer.OFFERED;

        return KeepsTooMuch(tokensWithMessage.Window.DefaultTokens, tokensWithMessage.Tokens, summarized.HistoryTokens)
            ? ChatCompressionOffer.KEEPS_TOO_MUCH
            : ChatCompressionOffer.OFFERED;
    }

    /// <summary>
    /// Decides what speaks against compressing before anything is counted.
    /// </summary>
    /// <remarks>
    /// The model has to be large enough for a summary, see ConversationSummaryBudget.CanSummarizeWith. The message
    /// about to be sent goes along unchanged, so it has to leave room for the summary by itself; that is checked
    /// before whether there is something to summarize at all, since a message which does not fit would not fit
    /// after any compressing either.
    /// </remarks>
    /// <param name="window">The context window of the model which would write the summary.</param>
    /// <param name="hasSomethingToSummarize">Whether the chat holds messages to summarize besides the last exchange, see ChatCompressionPlan.Of.</param>
    /// <param name="messageTokens">How many tokens the message about to be sent takes.</param>
    /// <returns>Whether compressing may be offered, as far as can be told without a count, or why not; NONE without a known window, since nothing then asks before sending.</returns>
    internal static ChatCompressionOffer OfferBeforeCounting(ContextWindow window, bool hasSomethingToSummarize, int messageTokens)
    {
        if (!window.IsKnown)
            return ChatCompressionOffer.NONE;

        if (!ConversationSummaryBudget.CanSummarizeWith(window))
            return ChatCompressionOffer.WINDOW_TOO_SMALL;

        if (messageTokens + ConversationSummaryBudget.TokensForTheSummary(window.DefaultTokens) >= window.DefaultTokens * ConversationTokens.SHARE_TO_ASK_BEFORE_SENDING)
            return ChatCompressionOffer.MESSAGE_TOO_LONG;

        return hasSomethingToSummarize ? ChatCompressionOffer.OFFERED : ChatCompressionOffer.NOTHING_TO_SUMMARIZE;
    }

    /// <summary>
    /// Whether what compressing keeps still fills the context window so much that AI Studio would ask again.
    /// </summary>
    /// <remarks>
    /// What the chat costs after compressing is what it costs now, less the summarized messages, plus the room for the
    /// summary. A large file attached to the last message of the user stays, for example, and so does a long system
    /// prompt.
    /// </remarks>
    /// <param name="windowTokens">The context window of the model.</param>
    /// <param name="tokensWithMessage">What the chat costs together with the message about to be sent.</param>
    /// <param name="summarizedTokens">What the messages which the summary replaces cost, with their files.</param>
    /// <returns>True when compressing would not make enough room.</returns>
    internal static bool KeepsTooMuch(int windowTokens, int tokensWithMessage, int summarizedTokens)
    {
        var tokensAfterCompressing = tokensWithMessage - summarizedTokens + ConversationSummaryBudget.TokensForTheSummary(windowTokens);
        return tokensAfterCompressing >= windowTokens * ConversationTokens.SHARE_TO_ASK_BEFORE_SENDING;
    }
}