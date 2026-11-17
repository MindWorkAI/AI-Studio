using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Decides how much room a chat of the BeyondFacts Lab has left in the context of its model.
/// </summary>
/// <remarks>
/// Every request carries the guide of the lab, so a conversation fills the context faster than an ordinary chat.
/// A model which loses the beginning of its context loses its instructions first; therefore, the lab stops before.
/// </remarks>
public static class BflTokenBudget
{
    /// <summary>
    /// From which share of the context on the lab warns.
    /// </summary>
    public const double WARNING_SHARE = 0.8;

    /// <summary>
    /// How many tokens the lab keeps free for the next message and its answer.
    /// </summary>
    public const int RESERVE_TOKENS = 4_096;

    /// <summary>
    /// Decides how much room a chat has left.
    /// </summary>
    /// <param name="tokens">What the conversation costs, with the system prompt, and the context of the model.</param>
    /// <returns>The state of the budget.</returns>
    public static BflTokenBudgetState Of(ConversationTokens tokens)
    {
        if (!tokens.IsKnown || !tokens.Window.IsKnown)
            return BflTokenBudgetState.UNKNOWN;

        var window = tokens.Window.DefaultTokens;
        if (tokens.Tokens > window - RESERVE_TOKENS)
            return BflTokenBudgetState.FULL;

        return tokens.Tokens >= window * WARNING_SHARE ? BflTokenBudgetState.WARNING : BflTokenBudgetState.OK;
    }
}