namespace AIStudio.Chat.Embedded;

/// <summary>
/// Decides how much room an embedded chat has left in the context of its model, see EmbeddedChat.ShowsContextWindow.
/// </summary>
/// <remarks>
/// Assistants whose requests carry long instructions, such as the guide of the BeyondFacts Lab, fill the context faster
/// than an ordinary chat, and a model which loses the beginning of its context loses its instructions first. Therefore,
/// the chat stops before. It does so as well where it hides the context window, e.g., in the Model Comparison: there,
/// the provider would refuse the request or cut the conversation instead.
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
    /// Decides how much of the room left a chat shows.
    /// </summary>
    /// <remarks>
    /// A chat which hides the context window, see EmbeddedChat.ShowsContextWindow, shows only that the conversation is
    /// full: a warning before would tell apart models the user must not know, while a conversation which does not fit
    /// anymore has to stop anyway, with a word why.
    /// </remarks>
    /// <param name="tokens">What the conversation costs, with the system prompt, and the context of the model.</param>
    /// <param name="showsContextWindow">Whether the chat shows the context window.</param>
    /// <returns>The state of the budget the chat shows.</returns>
    public static EmbeddedChatTokenBudgetState Shown(ConversationTokens tokens, bool showsContextWindow)
    {
        var state = Of(tokens);
        return showsContextWindow || state is EmbeddedChatTokenBudgetState.FULL ? state : EmbeddedChatTokenBudgetState.UNKNOWN;
    }

    /// <summary>
    /// Decides what the chat tells the user before it sends a message.
    /// </summary>
    /// <remarks>
    /// The chat refuses a message after which the conversation would be full, see RESERVE_TOKENS; before that, it asks
    /// from the share on from which the chat page asks as well, see ConversationTokens.SHARE_TO_ASK_BEFORE_SENDING. In
    /// a small window, the reserve comes first; in a large one, the share does. A chat which hides the context window
    /// does not ask, since its question would name the window; it only refuses, see Shown.
    /// </remarks>
    /// <param name="tokensWithMessage">What the conversation costs together with the message, and the context of the model.</param>
    /// <param name="showsContextWindow">Whether the chat shows the context window.</param>
    /// <returns>What to tell the user.</returns>
    public static EmbeddedChatSendNotice NoticeBeforeSending(ConversationTokens tokensWithMessage, bool showsContextWindow)
    {
        if (Of(tokensWithMessage) is EmbeddedChatTokenBudgetState.FULL)
            return EmbeddedChatSendNotice.FULL;

        return showsContextWindow && tokensWithMessage.AsksBeforeSending ? EmbeddedChatSendNotice.NEARLY_FULL : EmbeddedChatSendNotice.NONE;
    }
}