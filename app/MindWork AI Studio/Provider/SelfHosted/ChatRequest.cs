namespace AIStudio.Provider.SelfHosted;

/// <summary>
/// The chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="MaxTokens">The maximum number of tokens to generate.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<Message> Messages,
    bool Stream,
    
    int MaxTokens
);