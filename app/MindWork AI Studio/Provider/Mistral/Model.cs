using System.Text.Json.Serialization;

namespace AIStudio.Provider.Mistral;

/// <summary>
/// One model as Mistral lists it.
/// </summary>
/// <param name="Id">The model's ID.</param>
/// <param name="Object">What kind of thing the entry is. Known value: "model".</param>
/// <param name="Created">When the model was published, as seconds since the epoch.</param>
/// <param name="OwnedBy">Who Mistral names as the owner of the model.</param>
/// <param name="ContextWindowTokens">How much the model reads and writes in one conversation, in tokens.</param>
public readonly record struct Model(string Id, string Object, int Created, string OwnedBy, [property: JsonPropertyName("max_context_length")] int? ContextWindowTokens);