namespace AIStudio.Tools.Rust;

/// <summary>
/// The type of Tauri events we can receive.
/// </summary>
public enum TauriEventType
{
    NONE,
    PING,
    UNKNOWN,
    
    WINDOW_FOCUSED,
    WINDOW_NOT_FOCUSED,
    
    FILE_DROP_HOVERED,
    FILE_DROP_DROPPED,
    FILE_DROP_CANCELED,
}