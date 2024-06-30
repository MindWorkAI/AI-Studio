using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The Anthropic chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="System">The system prompt for the chat completion.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<Message> Messages,
    int MaxTokens,
    bool Stream,
    string System
);