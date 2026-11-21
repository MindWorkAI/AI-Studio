namespace AIStudio.Chat.Embedded;

/// <summary>
/// Decides how much room an embedded chat has left in the context of its model, see EmbeddedChat.ShowsContextWindow.
/// </summary>
/// <remarks>
/// Meant for assistants whose requests carry long instructions, such as the guide of the BeyondFacts Lab: their
/// conversations fill the context faster than an ordinary chat, and a model which loses the beginning of its context
/// loses its instructions first. Therefore, the chat stops before.
/// </remarks>
public static class EmbeddedChatTokenBudget
{
    /// <summary>
    /// From which share of the context on the chat warns.
    /// </summary>
    public const double WARNING_SHARE = 0.8;

    /// <summary>
    /// How many tokens the chat keeps free for the next message and its answer.
    /// </summary>
    public const int RESERVE_TOKENS = 4_096;

    /// <summary>
    /// Decides how much room a chat has left.
    /// </summary>
    /// <param name="tokens">What the conversation costs, with the system prompt, and the context of the model.</param>
    /// <returns>The state of the budget.</returns>
    public static EmbeddedChatTokenBudgetState Of(ConversationTokens tokens)
    {
        if (!tokens.IsKnown || !tokens.Window.IsKnown)
            return EmbeddedChatTokenBudgetState.UNKNOWN;

        var window = tokens.Window.DefaultTokens;
        if (tokens.Tokens > window - RESERVE_TOKENS)
            return EmbeddedChatTokenBudgetState.FULL;

        return tokens.Tokens >= window * WARNING_SHARE ? EmbeddedChatTokenBudgetState.WARNING : EmbeddedChatTokenBudgetState.OK;
    }

    /// <summary>
    /// Decides what the chat tells the user before it sends a message.
    /// </summary>
    /// <remarks>
    /// The chat refuses a message after which the conversation would be full, see RESERVE_TOKENS; before that, it asks
    /// from the share on from which the chat page asks as well, see ConversationTokens.SHARE_TO_ASK_BEFORE_SENDING. In
    /// a small window, the reserve comes first; in a large one, the share does.
    /// </remarks>
    /// <param name="tokensWithMessage">What the conversation costs together with the message, and the context of the model.</param>
    /// <returns>What to tell the user.</returns>
    public static EmbeddedChatSendNotice NoticeBeforeSending(ConversationTokens tokensWithMessage)
    {
        if (Of(tokensWithMessage) is EmbeddedChatTokenBudgetState.FULL)
            return EmbeddedChatSendNotice.FULL;

        return tokensWithMessage.AsksBeforeSending ? EmbeddedChatSendNotice.NEARLY_FULL : EmbeddedChatSendNotice.NONE;
    }
}