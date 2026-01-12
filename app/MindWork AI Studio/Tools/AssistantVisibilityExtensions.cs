using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools;

/// <summary>
/// Extension methods for checking assistant visibility based on configuration and preview features.
/// </summary>
public static class AssistantVisibilityExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(AssistantVisibilityExtensions));

    /// <summary>
    /// Checks if an assistant should be visible based on configuration and optional preview feature requirements.
    /// </summary>
    /// <param name="settingsManager">The settings manager to check configuration against.</param>
    /// <param name="withLogging">Whether to log visibility decisions.</param>
    /// <param name="assistantName">The name of the assistant to check (for logging purposes).</param>
    /// <param name="component">The assistant component to check.</param>
    /// <param name="requiredPreviewFeature">Optional preview feature that must be enabled for the assistant to be visible.</param>
    /// <returns>True if the assistant should be visible, false otherwise.</returns>
    public static bool IsAssistantVisible(this SettingsManager settingsManager, Components component, bool withLogging = true, string assistantName = "", PreviewFeatures requiredPreviewFeature = PreviewFeatures.NONE)
    {
        withLogging = withLogging && !string.IsNullOrWhiteSpace(assistantName);
        
        // Check if a preview feature is required and enabled:
        if (requiredPreviewFeature != PreviewFeatures.NONE && !requiredPreviewFeature.IsEnabled(settingsManager))
        {
            if(withLogging)
                LOGGER.LogInformation("Assistant '{AssistantName}' is not visible because the required preview feature '{PreviewFeature}' is not enabled.", assistantName, requiredPreviewFeature);
            
            return false;
        }

        // If no component is specified, it's always visible:
        if (component is Components.NONE)
        {
            if(withLogging)
                LOGGER.LogWarning("Assistant '{AssistantName}' is visible because no component is specified.", assistantName);
            
            return true;
        }

        // Map Components enum to ConfigurableAssistant enum:
        var configurableAssistant = component switch
        {
            Components.GRAMMAR_SPELLING_ASSISTANT => ConfigurableAssistant.GRAMMAR_SPELLING_ASSISTANT,
            Components.ICON_FINDER_ASSISTANT => ConfigurableAssistant.ICON_FINDER_ASSISTANT,
            Components.REWRITE_ASSISTANT => ConfigurableAssistant.REWRITE_ASSISTANT,
            Components.TRANSLATION_ASSISTANT => ConfigurableAssistant.TRANSLATION_ASSISTANT,
            Components.AGENDA_ASSISTANT => ConfigurableAssistant.AGENDA_ASSISTANT,
            Components.CODING_ASSISTANT => ConfigurableAssistant.CODING_ASSISTANT,
            Components.TEXT_SUMMARIZER_ASSISTANT => ConfigurableAssistant.TEXT_SUMMARIZER_ASSISTANT,
            Components.EMAIL_ASSISTANT => ConfigurableAssistant.EMAIL_ASSISTANT,
            Components.LEGAL_CHECK_ASSISTANT => ConfigurableAssistant.LEGAL_CHECK_ASSISTANT,
            Components.SYNONYMS_ASSISTANT => ConfigurableAssistant.SYNONYMS_ASSISTANT,
            Components.MY_TASKS_ASSISTANT => ConfigurableAssistant.MY_TASKS_ASSISTANT,
            Components.JOB_POSTING_ASSISTANT => ConfigurableAssistant.JOB_POSTING_ASSISTANT,
            Components.BIAS_DAY_ASSISTANT => ConfigurableAssistant.BIAS_DAY_ASSISTANT,
            Components.ERI_ASSISTANT => ConfigurableAssistant.ERI_ASSISTANT,
            Components.DOCUMENT_ANALYSIS_ASSISTANT => ConfigurableAssistant.DOCUMENT_ANALYSIS_ASSISTANT,
            Components.I18N_ASSISTANT => ConfigurableAssistant.I18N_ASSISTANT,

            _ => ConfigurableAssistant.UNKNOWN,
        };

        // If the component doesn't map to a configurable assistant, it's always visible:
        if (configurableAssistant is ConfigurableAssistant.UNKNOWN)
        {
            if(withLogging)
                LOGGER.LogWarning("Assistant '{AssistantName}' is visible because its component '{Component}' does not map to a configurable assistant.", assistantName, component);
            
            return true;
        }

        // Check if the assistant is hidden by any configuration plugin:
        var isHidden = settingsManager.ConfigurationData.App.HiddenAssistants.Contains(configurableAssistant);
        if (isHidden && withLogging)
            LOGGER.LogInformation("Assistant '{AssistantName}' is hidden based on the configuration.", assistantName);

        return !isHidden;
    }

    /// <summary>
    /// Checks if any assistant in a category should be visible.
    /// </summary>
    /// <param name="settingsManager">The settings manager to check configuration against.</param>
    /// <param name="categoryName">The name of the assistant category (for logging purposes).</param>
    /// <param name="assistants">The assistants in the category with their optional preview feature requirements.</param>
    /// <returns>True if at least one assistant in the category should be visible, false otherwise.</returns>
    public static bool IsAnyCategoryAssistantVisible(this SettingsManager settingsManager, string categoryName, params (Components Component, PreviewFeatures RequiredPreviewFeature)[] assistants)
    {
        foreach (var (component, requiredPreviewFeature) in assistants)
            if (settingsManager.IsAssistantVisible(component, withLogging: false, requiredPreviewFeature: requiredPreviewFeature))
                return true;

        LOGGER.LogInformation("No assistants in category '{CategoryName}' are visible.", categoryName);
        return false;
    }
}
