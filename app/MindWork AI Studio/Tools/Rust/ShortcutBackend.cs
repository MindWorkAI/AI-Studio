namespace AIStudio.Tools.Rust;

/// <summary>
/// Native backend used to register a global shortcut.
/// </summary>
public enum ShortcutBackend
{
    NONE,
    PORTAL,
    TAURI,
    LOCAL,
}
