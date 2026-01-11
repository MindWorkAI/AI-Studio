namespace AIStudio.Settings;

/// <summary>
/// Enum representing assistants that can be hidden via configuration plugin.
/// </summary>
public enum ConfigurableAssistant
{
    GRAMMAR_SPELLING_ASSISTANT,
    ICON_FINDER_ASSISTANT,
    REWRITE_ASSISTANT,
    TRANSLATION_ASSISTANT,
    AGENDA_ASSISTANT,
    CODING_ASSISTANT,
    TEXT_SUMMARIZER_ASSISTANT,
    EMAIL_ASSISTANT,
    LEGAL_CHECK_ASSISTANT,
    SYNONYMS_ASSISTANT,
    MY_TASKS_ASSISTANT,
    JOB_POSTING_ASSISTANT,
    BIAS_DAY_ASSISTANT,
    ERI_ASSISTANT,
    DOCUMENT_ANALYSIS_ASSISTANT,

    // ReSharper disable InconsistentNaming
    I18N_ASSISTANT,
    // ReSharper restore InconsistentNaming
}
