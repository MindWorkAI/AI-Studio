using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider.Groq;

/// <summary>
/// The Groq chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="Seed">The seed for the chat completion.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<Message> Messages,
    bool Stream,
    int Seed
);