using System.Text.Json.Serialization;

namespace AIStudio.Provider.Groq;

/// <summary>
/// One model as Groq lists it.
/// </summary>
/// <remarks>
/// Groq says more about a model than the shared OpenAI-compatible list does, which is why this
/// provider brings a data model of its own instead of using that one: the shared record is read by
/// a dozen providers, and a field only one of them sends has no business in it.
/// </remarks>
/// <param name="Id">The model's ID.</param>
/// <param name="ContextWindowTokens">How much the model reads and writes in one conversation, in tokens.</param>
public readonly record struct GroqModel(string Id, [property: JsonPropertyName("context_window")] int? ContextWindowTokens);