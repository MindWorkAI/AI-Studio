namespace AIStudio.Tools;

public enum Components
{
    NONE = 0,
    
    GRAMMAR_SPELLING_ASSISTANT,
    ICON_FINDER_ASSISTANT,
    REWRITE_ASSISTANT,
    PROMPT_OPTIMIZER_ASSISTANT,
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
    SLIDE_BUILDER_ASSISTANT,
    META_ASSISTANT,
    
    // ReSharper disable InconsistentNaming
    I18N_ASSISTANT,
    // ReSharper restore InconsistentNaming
    
    CHAT,

    // Internal identity for plugin-provided assistants. Its defaults are derived from CHAT,
    // but it remains separate from the built-in chat component and its session state.
    DYNAMIC_ASSISTANT,
    WRITER,
    APP_SETTINGS,
    
    AGENT_TEXT_CONTENT_CLEANER,
    AGENT_DATA_SOURCE_SELECTION,
    AGENT_RETRIEVAL_CONTEXT_VALIDATION,
    AGENT_ASSISTANT_PLUGIN_AUDIT,
    LOG_VIEWER_ASSISTANT,
    VISUAL_BRIEFING_ASSISTANT,
}
