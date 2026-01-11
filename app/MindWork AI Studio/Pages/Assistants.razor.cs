using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Pages;

public partial class Assistants : MSGComponentBase
{
    /// <summary>
    /// Checks if an assistant should be visible based on configuration.
    /// </summary>
    /// <param name="component">The assistant component to check.</param>
    /// <returns>True if the assistant should be visible, false otherwise.</returns>
    private bool IsAssistantVisible(Components component)
    {
        // Map Components enum to ConfigurableAssistant enum
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
            _ => (ConfigurableAssistant?)null,
        };

        // If the component doesn't map to a configurable assistant, it's always visible
        if (configurableAssistant is null)
            return true;

        // Check if the assistant is hidden in configuration
        return !this.SettingsManager.ConfigurationData.App.HiddenAssistants.Contains(configurableAssistant.Value);
    }
}