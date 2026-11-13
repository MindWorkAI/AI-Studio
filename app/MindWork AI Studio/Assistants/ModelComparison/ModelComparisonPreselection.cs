using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Decides which models a new preset or a new comparison series of the Model Comparison assistant starts with.
/// </summary>
/// <remarks>
/// SettingsManager.GetPreselectedProvider cannot decide this: it always falls back to the provider of the whole
/// app, while the options of this assistant may also preselect no model at all, e.g., because the user wants to
/// pick both models anew for each comparison.
/// </remarks>
public static class ModelComparisonPreselection
{
    /// <param name="settingsManager">The settings.</param>
    extension(SettingsManager settingsManager)
    {
        /// <summary>
        /// Gets the first model of a new preset or comparison series: the preselected model of the options, or the
        /// provider of the whole app without them.
        /// </summary>
        /// <remarks>
        /// A provider which the organization prohibited, or which misses the confidence requirements of the
        /// assistant, preselects no model.
        /// </remarks>
        public AIStudio.Settings.Provider PreselectedComparisonModel
        {
            get
            {
                var options = settingsManager.ConfigurationData.ModelComparison;
                return options.PreselectOptions
                    ? Resolve(settingsManager, options.PreselectedProvider)
                    : settingsManager.GetPreselectedProvider(Tools.Components.MODEL_COMPARISON_ASSISTANT);
            }
        }

        /// <summary>
        /// Gets the judge of a new preset or comparison series: the preselected judge model of the options, or no
        /// model without them. A new preset or comparison series switches its judge on only when this is a model.
        /// </summary>
        /// <remarks>
        /// A provider which the organization prohibited, or which misses the confidence requirements of the
        /// assistant, preselects no model.
        /// </remarks>
        public AIStudio.Settings.Provider PreselectedComparisonJudge
        {
            get
            {
                var options = settingsManager.ConfigurationData.ModelComparison;
                return options.PreselectOptions
                    ? Resolve(settingsManager, options.PreselectedJudgeProvider)
                    : AIStudio.Settings.Provider.NONE;
            }
        }
    }

    /// <summary>
    /// Resolves the value of a model option into a provider which the assistant may use.
    /// </summary>
    /// <param name="settingsManager">The settings.</param>
    /// <param name="providerId">The value of the option: DataModelComparison.NO_MODEL, an empty text for the
    /// provider of the whole app, or the ID of a provider.</param>
    /// <returns>The provider, or NONE when the option preselects no model or names one the assistant may not use.</returns>
    private static AIStudio.Settings.Provider Resolve(SettingsManager settingsManager, string providerId)
    {
        if (string.Equals(providerId, DataModelComparison.NO_MODEL, StringComparison.OrdinalIgnoreCase))
            return AIStudio.Settings.Provider.NONE;

        var provider = settingsManager.GetProviderById(string.IsNullOrWhiteSpace(providerId) ? settingsManager.ConfigurationData.App.PreselectedProvider : providerId);
        return settingsManager.IsProviderConfident(provider, Tools.Components.MODEL_COMPARISON_ASSISTANT)
            ? provider
            : AIStudio.Settings.Provider.NONE;
    }
}