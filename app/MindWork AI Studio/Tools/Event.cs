namespace AIStudio.Tools;

/// <summary>
/// Defines message bus events used for communication between UI components and services.
/// </summary>
public enum Event
{
    /// <summary>
    /// Represents the absence of a message bus event.
    /// </summary>
    NONE,
    
    
    
    
    
    //
    // Common events:
    //
    
    /// <summary>
    /// Requests registered receivers to refresh state that depends on the current UI state.
    /// </summary>
    STATE_HAS_CHANGED,

    /// <summary>
    /// Notifies receivers that the application configuration was changed and should be reloaded or re-applied.
    /// </summary>
    CONFIGURATION_CHANGED,

    /// <summary>
    /// Notifies receivers that the active color theme changed.
    /// </summary>
    COLOR_THEME_CHANGED,

    /// <summary>
    /// Requests startup initialization of the plugin system.
    /// </summary>
    STARTUP_PLUGIN_SYSTEM,

    /// <summary>
    /// Notifies receivers that the startup initialization completed.
    /// </summary>
    STARTUP_COMPLETED,

    /// <summary>
    /// Carries an enterprise environment that should be processed during startup.
    /// </summary>
    STARTUP_ENTERPRISE_ENVIRONMENT,

    /// <summary>
    /// Notifies receivers that the known enterprise environments changed.
    /// </summary>
    ENTERPRISE_ENVIRONMENTS_CHANGED,

    /// <summary>
    /// Notifies receivers that plugins were reloaded.
    /// </summary>
    PLUGINS_RELOADED,

    /// <summary>
    /// Requests display of an error notification.
    /// </summary>
    SHOW_ERROR,

    /// <summary>
    /// Requests display of a warning notification.
    /// </summary>
    SHOW_WARNING,

    /// <summary>
    /// Requests display of a success notification.
    /// </summary>
    SHOW_SUCCESS,

    /// <summary>
    /// Requests display of a prompt-injection alert dialog.
    /// </summary>
    SHOW_PROMPT_INJECTION_ALERT,

    /// <summary>
    /// Carries an event received from the Tauri runtime.
    /// </summary>
    TAURI_EVENT_RECEIVED,

    /// <summary>
    /// Notifies receivers that the Rust service is unavailable or failed a health check.
    /// </summary>
    RUST_SERVICE_UNAVAILABLE,

    /// <summary>
    /// Notifies receivers that voice recording availability changed.
    /// </summary>
    VOICE_RECORDING_AVAILABILITY_CHANGED,
    
    // Update events:
    /// <summary>
    /// Requests a user-triggered search for application updates.
    /// </summary>
    USER_SEARCH_FOR_UPDATE,

    /// <summary>
    /// Notifies receivers that an application update is available.
    /// </summary>
    UPDATE_AVAILABLE,

    /// <summary>
    /// Requests installation of the available application update.
    /// </summary>
    INSTALL_UPDATE,
    
    
    
    //
    // Chat events:
    //
    
    /// <summary>
    /// Queries whether the current chat has unsaved changes.
    /// </summary>
    HAS_CHAT_UNSAVED_CHANGES,

    /// <summary>
    /// Requests the current chat state to be reset.
    /// </summary>
    RESET_CHAT_STATE,

    /// <summary>
    /// Carries a chat that should be loaded by the chat component.
    /// </summary>
    LOAD_CHAT,

    /// <summary>
    /// Notifies receivers that chat response streaming has completed.
    /// </summary>
    CHAT_STREAMING_DONE,

    /// <summary>
    /// Notifies receivers that an AI job changed.
    /// </summary>
    AI_JOB_CHANGED,

    /// <summary>
    /// Notifies receivers that an AI job finished.
    /// </summary>
    AI_JOB_FINISHED,

    /// <summary>
    /// Notifies receivers that chat generation state changed.
    /// </summary>
    CHAT_GENERATION_CHANGED,

    /// <summary>
    /// Notifies receivers that an assistant session changed.
    /// </summary>
    ASSISTANT_SESSION_CHANGED,

    /// <summary>
    /// Notifies receivers that an assistant session finished.
    /// </summary>
    ASSISTANT_SESSION_FINISHED,
    
    // Workspace events:
    /// <summary>
    /// Notifies receivers that the chat loaded in the workspace changed.
    /// </summary>
    WORKSPACE_LOADED_CHAT_CHANGED,

    /// <summary>
    /// Requests the chat workspace overlay to be toggled.
    /// </summary>
    WORKSPACE_TOGGLE_OVERLAY,

    /// <summary>
    /// Notifies receivers that a workspace was renamed.
    /// </summary>
    WORKSPACE_RENAMED,

    /// <summary>
    /// Notifies receivers that a workspace was created.
    /// </summary>
    WORKSPACE_CREATED,
    
    
    
    
    
    //
    // RAG events:
    //
    
    /// <summary>
    /// Carries data sources that were automatically selected for retrieval-augmented generation.
    /// </summary>
    RAG_AUTO_DATA_SOURCES_SELECTED,
    
    
    
    
    
    //
    // File attachment events:
    //
    
    /// <summary>
    /// Registers a file drop area for file attachment handling.
    /// </summary>
    REGISTER_FILE_DROP_AREA,

    /// <summary>
    /// Unregisters a file drop area from file attachment handling.
    /// </summary>
    UNREGISTER_FILE_DROP_AREA,
    
    
    
    
    //
    // Send events:
    //
    
    /// <summary>
    /// Sends content to the grammar and spelling assistant.
    /// </summary>
    SEND_TO_GRAMMAR_SPELLING_ASSISTANT,

    /// <summary>
    /// Sends content to the icon finder assistant.
    /// </summary>
    SEND_TO_ICON_FINDER_ASSISTANT,

    /// <summary>
    /// Sends content to the rewrite assistant.
    /// </summary>
    SEND_TO_REWRITE_ASSISTANT,

    /// <summary>
    /// Sends content to the prompt optimizer assistant.
    /// </summary>
    SEND_TO_PROMPT_OPTIMIZER_ASSISTANT,

    /// <summary>
    /// Sends content to the translation assistant.
    /// </summary>
    SEND_TO_TRANSLATION_ASSISTANT,

    /// <summary>
    /// Sends content to the agenda assistant.
    /// </summary>
    SEND_TO_AGENDA_ASSISTANT,

    /// <summary>
    /// Sends content to the coding assistant.
    /// </summary>
    SEND_TO_CODING_ASSISTANT,

    /// <summary>
    /// Sends content to the text summarizer assistant.
    /// </summary>
    SEND_TO_TEXT_SUMMARIZER_ASSISTANT,

    /// <summary>
    /// Sends the result of the current assistant to the chat component.
    /// </summary>
    SEND_TO_CHAT,

    /// <summary>
    /// Sends text to the chat input field, aka the user prompt.
    /// </summary>
    SEND_TO_CHAT_INPUT,

    /// <summary>
    /// Sends content to the email assistant.
    /// </summary>
    SEND_TO_EMAIL_ASSISTANT,

    /// <summary>
    /// Sends content to the legal check assistant.
    /// </summary>
    SEND_TO_LEGAL_CHECK_ASSISTANT,

    /// <summary>
    /// Sends content to the synonym assistant.
    /// </summary>
    SEND_TO_SYNONYMS_ASSISTANT,

    /// <summary>
    /// Sends content to the "my tasks assistant".
    /// </summary>
    SEND_TO_MY_TASKS_ASSISTANT,

    /// <summary>
    /// Sends content to the job posting assistant.
    /// </summary>
    SEND_TO_JOB_POSTING_ASSISTANT,

    /// <summary>
    /// Sends content to the document analysis assistant.
    /// </summary>
    SEND_TO_DOCUMENT_ANALYSIS_ASSISTANT,

    /// <summary>
    /// Sends content to the slide builder assistant.
    /// </summary>
    SEND_TO_SLIDE_BUILDER_ASSISTANT
}