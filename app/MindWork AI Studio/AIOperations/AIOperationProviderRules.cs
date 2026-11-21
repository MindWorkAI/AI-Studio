using AIStudio.Provider;

namespace AIStudio.AIOperations;

/// <summary>
/// The confidence levels which a provider has to reach for an AI operation.
/// </summary>
/// <param name="GlobalMinimumConfidence">The global minimum, when the user or the organization enforces one. It applies to every candidate.</param>
/// <param name="OwnMinimumConfidence">The minimum of the operation itself, from its settings.</param>
/// <param name="RequiredConfidence">What the content of the request requires, e.g., its data sources. It applies to every candidate.</param>
/// <param name="OwnMinimumAppliesToFallbacks">Whether the minimum of the operation applies to the fallbacks as well, not only to its preferred provider.</param>
public sealed record AIOperationProviderRules(ConfidenceLevel GlobalMinimumConfidence, ConfidenceLevel OwnMinimumConfidence, ConfidenceLevel RequiredConfidence, bool OwnMinimumAppliesToFallbacks);
