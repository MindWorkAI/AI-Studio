using System.ComponentModel.DataAnnotations;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// The OpenAI chat request model.
/// </summary>
/// <param name="Model">Which model to use for chat completion.</param>
/// <param name="Messages">The chat messages.</param>
/// <param name="Stream">Whether to stream the chat completion.</param>
/// <param name="Seed">The seed for the chat completion.</param>
/// <param name="FrequencyPenalty">The frequency penalty for the chat completion.</param>
public readonly record struct ChatRequest(
    string Model,
    IList<Message> Messages,
    bool Stream,
    int Seed,
    
    [Range(-2.0f, 2.0f)]
    float FrequencyPenalty
);