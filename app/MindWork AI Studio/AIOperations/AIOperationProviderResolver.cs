using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.AIOperations;

/// <summary>
/// Chooses the provider for a call of an AI operation.
/// </summary>
/// <remarks>
/// It gathers the facts about each candidate from the settings, fresh on every call, and leaves the decision
/// to AIOperationProviderChoice.Choose. Every caller which runs an operation chooses through it, and so does
/// every check which needs to know in advance which provider would see the content, so that the two never
/// drift apart.
/// </remarks>
public sealed class AIOperationProviderResolver(SettingsManager settingsManager, ILogger<AIOperationProviderResolver> logger)
{
    /// <summary>
    /// Chooses the provider for running an operation with the given request, and logs the choice.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    /// <param name="request">The request to run it with.</param>
    /// <param name="settings">The provider settings of the operation, from its GetProviderSettings.</param>
    /// <param name="options">What the caller adds: the fallbacks, whether the minimum of the operation applies to them, and a check of its own.</param>
    /// <returns>The chosen provider with the reasons why the others were not chosen.</returns>
    public AIOperationProviderChoice Resolve<TRequest, TResult>(IAIOperation<TRequest, TResult> operation, TRequest request, AIOperationProviderSettings settings, AIOperationProviderOptions options)
    {
        var choice = this.Preview(settings, operation.GetRequiredConfidence(request), options);
        if (choice.HasProvider)
        {
            var provider = choice.Provider;
            var fallbackNote = choice.UsesFallback ? $" as a fallback, since its preferred provider was {choice.PreferredIssue}" : string.Empty;
            logger.LogInformation($"The AI operation '{operation.Id}' uses the provider '{provider.InstanceName}' ({provider.UsedLLMProvider.Name}, confidence={provider.UsedLLMProvider.GetConfidence(settingsManager).Level.Name}){fallbackNote}.");
        }
        else
            logger.LogWarning($"The AI operation '{operation.Id}' has no provider: its preferred provider was {choice.PreferredIssue}, its first fallback was {choice.FallbackIssue}.");

        return choice;
    }

    /// <summary>
    /// Chooses the provider which would run an operation, without logging the choice.
    /// </summary>
    /// <remarks>
    /// For checks in advance, e.g., which providers get to see the data of a data source.
    /// </remarks>
    /// <param name="settings">The provider settings of the operation, from its GetProviderSettings.</param>
    /// <param name="requiredConfidence">The confidence level the content of the request requires; NONE when it is not known yet.</param>
    /// <param name="options">What the caller adds: the fallbacks, whether the minimum of the operation applies to them, and a check of its own.</param>
    /// <returns>The chosen provider with the reasons why the others were not chosen.</returns>
    public AIOperationProviderChoice Preview(AIOperationProviderSettings settings, ConfidenceLevel requiredConfidence, AIOperationProviderOptions options)
    {
        var preferred = this.Describe(settings.PreferredProviderId, options.CheckProvider);
        var fallbacks = options.FallbackProviderIds.Select(id => this.Describe(id, options.CheckProvider)).ToList();
        var rules = new AIOperationProviderRules(settingsManager.GlobalMinimumConfidenceLevel, settings.MinimumConfidence, requiredConfidence, options.OwnMinimumConfidenceAppliesToFallbacks);
        return AIOperationProviderChoice.Choose(preferred, fallbacks, rules);
    }

    /// <summary>
    /// Gathers the facts about the provider with the given ID.
    /// </summary>
    private AIOperationProviderCandidate Describe(string? providerId, Func<Settings.Provider, AIOperationProviderIssue>? checkProvider)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.Equals(providerId, Settings.Provider.NONE.Id, StringComparison.OrdinalIgnoreCase))
            return AIOperationProviderCandidate.NOT_NAMED;

        var provider = settingsManager.GetProviderById(providerId);
        if (provider == Settings.Provider.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
            return AIOperationProviderCandidate.MISSING;

        return new AIOperationProviderCandidate(
            provider,
            true,
            true,
            settingsManager.IsProviderProhibited(provider.UsedLLMProvider),
            provider.UsedLLMProvider.GetConfidence(settingsManager).Level,
            checkProvider?.Invoke(provider) ?? AIOperationProviderIssue.NONE);
    }
}