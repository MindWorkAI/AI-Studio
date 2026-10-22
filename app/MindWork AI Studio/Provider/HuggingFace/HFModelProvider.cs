using System.Text.Json.Serialization;

namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// One inference provider serving a model.
/// </summary>
/// <param name="Provider">The slug of the inference provider, e.g. "novita".</param>
/// <param name="Status">Whether the provider currently serves the model. Known value: "live".</param>
/// <param name="ContextWindowTokens">How much this provider reads and writes in one conversation, in tokens.</param>
public readonly record struct HFModelProvider(string Provider, string Status, [property: JsonPropertyName("context_length")] int? ContextWindowTokens)
{
    private const string LIVE = "live";

    /// <summary>
    /// Whether this provider serves the model right now.
    /// </summary>
    public bool IsLive => string.Equals(this.Status, LIVE, StringComparison.OrdinalIgnoreCase);
}