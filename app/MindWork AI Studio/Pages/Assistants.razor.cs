using AIStudio.Components;
using AIStudio.Settings;

namespace AIStudio.Pages;

public partial class Assistants : MSGComponentBase
{
    /// <summary>
    /// Checks if an assistant should be visible based on configuration.
    /// </summary>
    /// <param name="component">The assistant component to check.</param>
    /// <returns>True if the assistant should be visible, false otherwise.</returns>
    private bool IsAssistantVisible(Tools.Components component)
    {
        // Map Components enum to ConfigurableAssistant enum:
        var configurableAssistant = component switch
        {
            Tools.Components.GRAMMAR_SPELLING_ASSISTANT => ConfigurableAssistant.GRAMMAR_SPELLING_ASSISTANT,
            Tools.Components.ICON_FINDER_ASSISTANT => ConfigurableAssistant.ICON_FINDER_ASSISTANT,
            Tools.Components.REWRITE_ASSISTANT => ConfigurableAssistant.REWRITE_ASSISTANT,
            Tools.Components.TRANSLATION_ASSISTANT => ConfigurableAssistant.TRANSLATION_ASSISTANT,
            Tools.Components.AGENDA_ASSISTANT => ConfigurableAssistant.AGENDA_ASSISTANT,
            Tools.Components.CODING_ASSISTANT => ConfigurableAssistant.CODING_ASSISTANT,
            Tools.Components.TEXT_SUMMARIZER_ASSISTANT => ConfigurableAssistant.TEXT_SUMMARIZER_ASSISTANT,
            Tools.Components.EMAIL_ASSISTANT => ConfigurableAssistant.EMAIL_ASSISTANT,
            Tools.Components.LEGAL_CHECK_ASSISTANT => ConfigurableAssistant.LEGAL_CHECK_ASSISTANT,
            Tools.Components.SYNONYMS_ASSISTANT => ConfigurableAssistant.SYNONYMS_ASSISTANT,
            Tools.Components.MY_TASKS_ASSISTANT => ConfigurableAssistant.MY_TASKS_ASSISTANT,
            Tools.Components.JOB_POSTING_ASSISTANT => ConfigurableAssistant.JOB_POSTING_ASSISTANT,
            Tools.Components.BIAS_DAY_ASSISTANT => ConfigurableAssistant.BIAS_DAY_ASSISTANT,
            Tools.Components.ERI_ASSISTANT => ConfigurableAssistant.ERI_ASSISTANT,
            Tools.Components.DOCUMENT_ANALYSIS_ASSISTANT => ConfigurableAssistant.DOCUMENT_ANALYSIS_ASSISTANT,
            Tools.Components.I18N_ASSISTANT => ConfigurableAssistant.I18N_ASSISTANT,
            
            _ => ConfigurableAssistant.UNKNOWN,
        };

        // If the component doesn't map to a configurable assistant, it's always visible:
        if (configurableAssistant is ConfigurableAssistant.UNKNOWN)
            return true;

        // Check if the assistant is hidden by any configuration plugin:
        return !this.SettingsManager.ConfigurationData.App.HiddenAssistants.Contains(configurableAssistant);
    }
}