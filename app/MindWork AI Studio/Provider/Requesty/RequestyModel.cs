using System.Text.Json.Serialization;

namespace AIStudio.Provider.Requesty;

/// <summary>
/// A data model for a Requesty model from the API.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="ContextWindowTokens">How much the model reads and writes in one conversation, in tokens.</param>
public readonly record struct RequestyModel(string Id, [property: JsonPropertyName("context_window")] int? ContextWindowTokens);