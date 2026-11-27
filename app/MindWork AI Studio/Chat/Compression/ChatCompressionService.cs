using AIStudio.AIOperations;
using AIStudio.AIOperations.ConversationSummary;
using AIStudio.Models;
using AIStudio.Settings;
using AIStudio.Tools.Services;

namespace AIStudio.Chat.Compression;

/// <summary>
/// Decides which model writes the summary of a chat, and whether compressing would make enough room in the context
/// window of the model of the chat.
/// </summary>
/// <remarks>
/// The summary itself is written by an AI operation, see ConversationSummaryOperation. This service holds what
/// belongs to the chat: which model may write its summary, and whether the notice before sending offers to compress
/// it. It keeps nothing of a call, so several chats may ask at the same time.
/// </remarks>
/// <param name="tokenCounter">Counts the tokens of the messages, with the tokenizer of the provider.</param>
/// <param name="providerResolver">Chooses the model which writes the summary.</param>
/// <param name="settingsManager">The settings, for the model picked for compressing.</param>
public sealed class ChatCompressionService(ConversationTokenCounter tokenCounter, AIOperationProviderResolver providerResolver, SettingsManager settingsManager)
{
    /// <summary>
    /// What compressing a chat adds to the choice of the model which writes the summary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model picked for compressing writes the summary when it may. Otherwise, the model of the chat does: it
    /// saw everything the summary reads already, so the minimum confidence of compressing does not apply to it.
    /// </para>
    /// <para>
    /// Both have to be allowed in the chat, with its confidence, the security of its data sources, and what the
    /// organization prohibited, see ChatThreadExtensions.IsLLMProviderAllowed, and their model has to be large
    /// enough for a summary, see ConversationSummaryBudget.CanSummarizeWith. Whether a model can see images does
    /// not matter: the summary reads no image, see ConversationSummaryOperation.RunAsync.
    /// </para>
    /// <para>
    /// Every place which runs the summary or tells about it in advance builds the options here, so that the model
    /// it names is the one which writes the summary.
    /// </para>
    /// </remarks>
    /// <param name="chat">The chat to summarize.</param>
    /// <param name="chatProvider">The provider of the chat, which continues with the summary.</param>
    /// <returns>The options for choosing the model which writes the summary.</returns>
    public static AIOperationProviderOptions GetProviderOptions(ChatThread chat, AIStudio.Settings.Provider chatProvider) => new()
    {
        FallbackProviderIds = [chatProvider.Id],
        OwnMinimumConfidenceAppliesToFallbacks = false,
        CheckProvider = provider => CheckWriter(chat, provider),
    };

    /// <summary>
    /// Whether a provider may write the summary of a chat, as far as the chat and the size of its model decide.
    /// </summary>
    /// <param name="chat">The chat to summarize.</param>
    /// <param name="provider">The provider which would write the summary.</param>
    /// <returns>NONE when it may, NOT_ALLOWED_FOR_CONTENT when the chat does not allow it, CONTEXT_WINDOW_TOO_SMALL when its model is too small.</returns>
    internal static AIOperationProviderIssue CheckWriter(ChatThread chat, AIStudio.Settings.Provider provider)
    {
        if (!chat.IsLLMProviderAllowed(provider))
            return AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT;

        return ConversationSummaryBudget.CanSummarizeWith(provider.GetModelProfile().Context)
            ? AIOperationProviderIssue.NONE
            : AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL;
    }

    /// <summary>
    /// Chooses the model which would write the summary of a chat, without logging the choice.
    /// </summary>
    /// <remarks>
    /// For the notice before sending, the button of the chat, and the dialog, which tells when the model of the chat
    /// writes the summary instead of the one picked for it. Running the summary chooses again, see
    /// AIOperationProviderResolver.Resolve, since the settings may have changed in between.
    /// </remarks>
    /// <param name="chat">The chat to summarize.</param>
    /// <param name="chatProvider">The provider of the chat, which continues with the summary.</param>
    /// <returns>The chosen provider with the reasons why the others were not chosen.</returns>
    public AIOperationProviderChoice ChooseWriter(ChatThread chat, AIStudio.Settings.Provider chatProvider) => providerResolver.Preview(ConversationSummaryOperation.GetProviderSettings(settingsManager), chat.RequiredProviderConfidence, GetProviderOptions(chat, chatProvider));

    /// <summary>
    /// Decides whether the notice before sending offers to compress a chat, or why compressing would not help.
    /// </summary>
    /// <remarks>
    /// Compressing replaces only the messages the plan summarizes, see ChatCompressionPlan.Of: the system prompt, the
    /// example conversation of the chat template, the last message of the user with its files, and the last answer of
    /// the AI stay word for word, and the message about to be sent goes along unchanged. So it helps only when what is
    /// left, together with the room for the summary, stays below the share from which AI Studio asks before sending,
    /// see ChatCompressionOptions.ShareToAsk. The checks which need no count come first, see
    /// OfferBeforeCounting. Then the summarized messages are counted with their files, which the count of the whole
    /// conversation mostly left in the cache of the counter already, and taken off that count, see KeepsTooMuch.
    /// When they cannot be counted, compressing is offered, since nothing speaks against it.<br/><br/>
    ///
    /// All of it is counted with the tokenizer of the provider of the chat, since it is about the context window of
    /// its model. The model which writes the summary only decides how long the summary may grow, see
    /// ConversationSummaryBudget.TokensForTheSummary.<br/><br/>
    ///
    /// Whether the place which asks allows compressing at all, e.g., with workspaces switched off, is left to that
    /// place: it asks only where it does.
    /// </remarks>
    /// <param name="chat">The chat to compress.</param>
    /// <param name="exampleConversationLength">How many blocks the example conversation of the chat template holds, 0 without a template.</param>
    /// <param name="chatProvider">The provider of the chat, with the tokenizer to count with.</param>
    /// <param name="tokensWithMessage">What the chat costs together with the message about to be sent, with the context window of the model.</param>
    /// <param name="options">The settings of compressing, read once for this decision.</param>
    /// <param name="token">Ends the count when nobody needs the answer anymore.</param>
    /// <returns>Whether compressing is offered, or why not.</returns>
    public async Task<ChatCompressionOffer> OfferBeforeSendingAsync(ChatThread chat, int exampleConversationLength, AIStudio.Settings.Provider chatProvider, ConversationTokens tokensWithMessage, ChatCompressionOptions options, CancellationToken token = default)
    {
        var writer = this.ChooseWriter(chat, chatProvider);
        ContextWindow? writerWindow = writer.HasProvider ? writer.Provider.GetModelProfile().Context : null;

        // The message is in the input field, so an unanswered last message is summarized rather than taken along:
        var plan = ChatCompressionPlan.Of(chat, exampleConversationLength, draftIsEmpty: false);
        var offer = OfferBeforeCounting(tokensWithMessage.Window, writerWindow, plan is not null, tokensWithMessage.DraftTokens, options);
        if (offer is not ChatCompressionOffer.OFFERED || plan is null || writerWindow is not { } window)
            return offer;

        var summarizedParts = ConversationParts.Of(new ChatThread { Blocks = [.. plan.ToSummarize] }, string.Empty, string.Empty, null, false, null);
        var summarized = await tokenCounter.CountAsync(chatProvider, summarizedParts, ReportedHistory.UNKNOWN, token);
        if (!summarized.IsKnown)
            return ChatCompressionOffer.OFFERED;

        var tokensForTheSummary = ConversationSummaryBudget.TokensForTheSummary(window, tokensWithMessage.Window, options.MaxSummaryTokens);
        return KeepsTooMuch(tokensWithMessage.Window.DefaultTokens, tokensWithMessage.Tokens, summarized.HistoryTokens, tokensForTheSummary, options.ShareToAsk)
            ? ChatCompressionOffer.KEEPS_TOO_MUCH
            : ChatCompressionOffer.OFFERED;
    }

    /// <summary>
    /// Decides what speaks against compressing before anything is counted.
    /// </summary>
    /// <remarks>
    /// There has to be a model which may write the summary, see GetProviderOptions; the only reason for none which
    /// the chat itself does not rule out already is a model too small for a summary. The message about to be sent
    /// goes along unchanged, so it has to leave room for the summary by itself; that is checked before whether
    /// there is something to summarize at all, since a message which does not fit would not fit after any
    /// compressing either.
    /// </remarks>
    /// <param name="chatWindow">The context window of the model of the chat.</param>
    /// <param name="writerWindow">The context window of the model which would write the summary, or null when no model may write it.</param>
    /// <param name="hasSomethingToSummarize">Whether the chat holds messages to summarize besides the last exchange, see ChatCompressionPlan.Of.</param>
    /// <param name="messageTokens">How many tokens the message about to be sent takes.</param>
    /// <param name="options">The settings of compressing: the share from which AI Studio asks, and the most tokens of a summary.</param>
    /// <returns>Whether compressing may be offered, as far as can be told without a count, or why not; NONE without a known window of the chat, since nothing then asks before sending.</returns>
    internal static ChatCompressionOffer OfferBeforeCounting(ContextWindow chatWindow, ContextWindow? writerWindow, bool hasSomethingToSummarize, int messageTokens, ChatCompressionOptions options)
    {
        if (!chatWindow.IsKnown)
            return ChatCompressionOffer.NONE;

        if (writerWindow is not { } window || !ConversationSummaryBudget.CanSummarizeWith(window))
            return ChatCompressionOffer.WINDOW_TOO_SMALL;

        var tokensForTheSummary = ConversationSummaryBudget.TokensForTheSummary(window, chatWindow, options.MaxSummaryTokens);
        if (messageTokens + tokensForTheSummary >= chatWindow.DefaultTokens * options.ShareToAsk)
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
    /// <param name="windowTokens">The context window of the model of the chat.</param>
    /// <param name="tokensWithMessage">What the chat costs together with the message about to be sent.</param>
    /// <param name="summarizedTokens">What the messages which the summary replaces cost, with their files.</param>
    /// <param name="tokensForTheSummary">The room kept for the summary, see ConversationSummaryBudget.TokensForTheSummary.</param>
    /// <param name="shareToAsk">The share of the context window from which AI Studio asks before sending.</param>
    /// <returns>True when compressing would not make enough room.</returns>
    internal static bool KeepsTooMuch(int windowTokens, int tokensWithMessage, int summarizedTokens, int tokensForTheSummary, double shareToAsk)
    {
        var tokensAfterCompressing = tokensWithMessage - summarizedTokens + tokensForTheSummary;
        return tokensAfterCompressing >= windowTokens * shareToAsk;
    }
}