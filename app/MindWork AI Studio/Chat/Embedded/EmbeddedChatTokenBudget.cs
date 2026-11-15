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
}