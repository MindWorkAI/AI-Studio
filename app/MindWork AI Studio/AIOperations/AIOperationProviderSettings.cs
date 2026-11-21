using AIStudio.Provider;

namespace AIStudio.AIOperations;

/// <summary>
/// The settings of an AI operation which decide its provider.
/// </summary>
/// <param name="PreferredProviderId">The ID of the provider picked for the operation; empty when nobody picked one or when the preselection is switched off.</param>
/// <param name="MinimumConfidence">The minimum confidence level of the operation itself.</param>
public sealed record AIOperationProviderSettings(string PreferredProviderId, ConfidenceLevel MinimumConfidence)
{
    /// <summary>
    /// No provider of its own and no minimum.
    /// </summary>
    public static readonly AIOperationProviderSettings NONE = new(string.Empty, ConfidenceLevel.NONE);
}