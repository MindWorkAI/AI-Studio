namespace AIStudio.Tools;

public enum Event
{
    NONE,
    
    // Common events:
    STATE_HAS_CHANGED,
    CONFIGURATION_CHANGED,
    
    // Update events:
    USER_SEARCH_FOR_UPDATE,
    UPDATE_AVAILABLE,
    
    // Chat events:
    HAS_CHAT_UNSAVED_CHANGES,
    RESET_CHAT_STATE,
}