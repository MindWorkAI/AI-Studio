using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Checks whether the Model Comparison assistant may use a provider, for both of its modes.
/// </summary>
public static class ModelComparisonProviderCheck
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(ModelComparisonProviderCheck).Namespace, nameof(ModelComparisonProviderCheck));

    /// <summary>
    /// Tells why the assistant may not use a provider which was selected, e.g., when it was saved in a preset or a
    /// comparison series: an organization may prohibit a provider or raise the confidence requirements at any time.
    /// </summary>
    /// <param name="settingsManager">The settings.</param>
    /// <param name="provider">The provider.</param>
    /// <returns>The reason, or null when the assistant may use the provider.</returns>
    public static string? IssueOf(SettingsManager settingsManager, AIStudio.Settings.Provider provider)
    {
        if (provider.UsedLLMProvider is LLMProviders.NONE)
            return TA("Please select a provider.");

        if (settingsManager.IsProviderProhibited(provider.UsedLLMProvider))
            return TA("The use of this provider has been prohibited by your organization. Please select a different provider.");

        if (!settingsManager.IsProviderConfident(provider, Tools.Components.MODEL_COMPARISON_ASSISTANT))
            return TA("This provider does not meet the confidence requirements of this assistant. Please choose another one.");

        return null;
    }
}