using AIStudio.Models;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a model needs to run the chats of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// Every chat of a step keeps the guide of the lab in the context, which alone takes about 20,000 tokens. A model
/// with a smaller context window would lose the beginning of each conversation, or fail right away. The conversation
/// about whether the lab fits a task leaves out the guide, so it runs with any model; its token budget tells the
/// person when it grows too long.
/// </remarks>
public static class BflModelRequirements
{
    /// <summary>
    /// The smallest context window the lab accepts, in tokens.
    /// </summary>
    /// <remarks>
    /// The fixed part of the longest system prompt may take about 22,000 tokens, see BflPromptLibraryTests; the
    /// rest is left for the details of the person and the conversation.
    /// </remarks>
    public const int MINIMUM_CONTEXT_TOKENS = 32_000;

    /// <summary>
    /// Checks whether a model reads enough at once for the lab.
    /// </summary>
    /// <remarks>
    /// A window nobody knows is accepted: many self-hosted models have no statement, and the token budget of the chat
    /// tells the person when a conversation grows too long.
    /// </remarks>
    /// <param name="contextWindow">The context window of the model.</param>
    /// <returns>True when the window is large enough or unknown.</returns>
    public static bool HasEnoughContext(ContextWindow contextWindow) => !contextWindow.IsKnown || contextWindow.DefaultTokens >= MINIMUM_CONTEXT_TOKENS;

    /// <summary>
    /// Checks whether a model reads enough at once for a chat of the lab.
    /// </summary>
    /// <param name="location">Where the chat belongs.</param>
    /// <param name="contextWindow">The context window of the model.</param>
    /// <returns>True when the chat needs no minimum, or when the window is large enough or unknown.</returns>
    public static bool HasEnoughContext(BflThreadLocation location, ContextWindow contextWindow) => location is BflOnboardingThreadLocation || HasEnoughContext(contextWindow);
}