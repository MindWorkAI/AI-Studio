using AIStudio.Provider;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What tells one model apart from another across stored comparisons, see ModelComparisonModelIdentity.
/// </summary>
/// <param name="LLMProvider">The kind of provider which ran the model.</param>
/// <param name="Hostname">The address of the server of a self-hosted model in lowercase, without a trailing slash; empty for every other provider.</param>
/// <param name="ModelId">The ID of the model at its provider.</param>
public readonly record struct ModelComparisonModelKey(LLMProviders LLMProvider, string Hostname, string ModelId) : IComparable<ModelComparisonModelKey>
{
    /// <summary>
    /// Gets the key of a model, with its host written the same way however the user typed it.
    /// </summary>
    /// <param name="llmProvider">The kind of provider which ran the model.</param>
    /// <param name="hostname">The address of the server of a self-hosted model, or empty.</param>
    /// <param name="modelId">The ID of the model at its provider.</param>
    /// <returns>The key.</returns>
    public static ModelComparisonModelKey Of(LLMProviders llmProvider, string hostname, string modelId) => new(llmProvider, hostname.Trim().TrimEnd('/').ToLowerInvariant(), modelId);

    /// <summary>
    /// Orders keys the same way on every computer, so that a pair of models is always written in the same order.
    /// </summary>
    /// <param name="other">The other key.</param>
    public int CompareTo(ModelComparisonModelKey other)
    {
        var byProvider = this.LLMProvider.CompareTo(other.LLMProvider);
        if (byProvider != 0)
            return byProvider;

        var byHost = string.CompareOrdinal(this.Hostname, other.Hostname);
        return byHost != 0 ? byHost : string.CompareOrdinal(this.ModelId, other.ModelId);
    }
}