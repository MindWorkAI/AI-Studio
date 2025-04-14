namespace AIStudio.Tools;

public enum Event
{
    NONE,
    
    // Common events:
    STATE_HAS_CHANGED,
    CONFIGURATION_CHANGED,
    COLOR_THEME_CHANGED,
    STARTUP_PLUGIN_SYSTEM,
    PLUGINS_RELOADED,
    SHOW_ERROR,
    SHOW_SUCCESS,
    
    // Update events:
    USER_SEARCH_FOR_UPDATE,
    UPDATE_AVAILABLE,
    
    // Chat events:
    HAS_CHAT_UNSAVED_CHANGES,
    RESET_CHAT_STATE,
    LOAD_CHAT,
    CHAT_STREAMING_DONE,
    
    // Workspace events:
    WORKSPACE_LOADED_CHAT_CHANGED,
    WORKSPACE_TOGGLE_OVERLAY,
    
    // RAG events:
    RAG_AUTO_DATA_SOURCES_SELECTED,
    
    // Send events:
    SEND_TO_GRAMMAR_SPELLING_ASSISTANT,
    SEND_TO_ICON_FINDER_ASSISTANT,
    SEND_TO_REWRITE_ASSISTANT,
    SEND_TO_TRANSLATION_ASSISTANT,
    SEND_TO_AGENDA_ASSISTANT,
    SEND_TO_CODING_ASSISTANT,
    SEND_TO_TEXT_SUMMARIZER_ASSISTANT,
    SEND_TO_CHAT,
    SEND_TO_EMAIL_ASSISTANT,
    SEND_TO_LEGAL_CHECK_ASSISTANT,
    SEND_TO_SYNONYMS_ASSISTANT,
    SEND_TO_MY_TASKS_ASSISTANT,
    SEND_TO_JOB_POSTING_ASSISTANT,
}