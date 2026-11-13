using AIStudio.Provider;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Which model took part in a stored comparison.
/// </summary>
/// <remarks>
/// The statistics count a model across comparisons by its provider, its host, and its ID, not by the provider entry
/// of the settings: the user may delete or rename that entry, and two entries may point to the same model. Only a
/// self-hosted model has a host of its own; two servers may run models of the same name which are not the same.
/// </remarks>
public sealed record ModelComparisonModelIdentity
{
    /// <summary>
    /// The kind of provider which ran the model.
    /// </summary>
    public LLMProviders LLMProvider { get; set; } = LLMProviders.NONE;

    /// <summary>
    /// The address of the server of a self-hosted model; empty for every other provider.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the model at its provider.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// The name the comparison showed for the model, for the statistics to show it again.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets the identity of the model of a provider entry.
    /// </summary>
    /// <param name="provider">The provider entry, with its model.</param>
    /// <returns>The identity.</returns>
    public static ModelComparisonModelIdentity Of(AIStudio.Settings.Provider provider) => new()
    {
        LLMProvider = provider.UsedLLMProvider,
        Hostname = provider.IsSelfHosted ? provider.Hostname : string.Empty,
        ModelId = provider.Model.Id,
        Label = provider.ToString(),
    };

    /// <summary>
    /// Whether both identities name the same model, whatever the label.
    /// </summary>
    /// <param name="other">The other identity.</param>
    public bool IsSameModelAs(ModelComparisonModelIdentity other) => this.LLMProvider == other.LLMProvider
        && string.Equals(this.Hostname, other.Hostname, StringComparison.OrdinalIgnoreCase)
        && string.Equals(this.ModelId, other.ModelId, StringComparison.Ordinal);
}