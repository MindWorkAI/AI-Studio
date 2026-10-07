using System.Text.Json.Serialization;

namespace AIStudio.Provider.OpenRouter;

/// <summary>
/// A data model for an OpenRouter model from the API.
/// </summary>
/// <remarks>
/// The window is the model's, not that of any one provider behind it. OpenRouter also states a
/// window per provider it currently prefers, but it picks one per request, so a number taken from
/// there would describe a choice nobody has made yet.
/// </remarks>
/// <param name="Id">The model's ID.</param>
/// <param name="Name">The model's human-readable display name.</param>
/// <param name="ContextWindowTokens">How much the model reads and writes in one conversation, in tokens.</param>
public readonly record struct OpenRouterModel(string Id, string? Name, [property: JsonPropertyName("context_length")] int? ContextWindowTokens);