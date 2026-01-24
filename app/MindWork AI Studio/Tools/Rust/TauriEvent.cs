namespace AIStudio.Tools.Rust;

/// <summary>
/// The data structure for a Tauri event sent from the Rust backend to the C# frontend.
/// </summary>
/// <param name="EventType">The type of the Tauri event.</param>
/// <param name="Payload">The payload of the Tauri event.</param>
public readonly record struct TauriEvent(TauriEventType EventType, List<string> Payload)
{
    /// <summary>
    /// Attempts to parse the first payload element as a shortcut.
    /// </summary>
    /// <param name="shortcut">The parsed shortcut name if successful.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    public bool TryGetShortcut(out Shortcut shortcut)
    {
        shortcut = default;
        if(this.EventType != TauriEventType.GLOBAL_SHORTCUT_PRESSED)
            return false;
        
        if (this.Payload.Count == 0)
            return false;

        // Try standard enum parsing (handles PascalCase and numeric values):
        if (Enum.TryParse(this.Payload[0], ignoreCase: true, out shortcut))
            return true;

        // Try parsing snake_case format (e.g., "voice_recording_toggle"):
        return TryParseSnakeCase(this.Payload[0], out shortcut);
    }

    /// <summary>
    /// Tries to parse a snake_case string into a ShortcutName enum value.
    /// </summary>
    private static bool TryParseSnakeCase(string value, out Shortcut shortcut)
    {
        shortcut = default;

        // Convert snake_case to UPPER_SNAKE_CASE for enum matching:
        var upperSnakeCase = value.ToUpperInvariant();

        // Try to match against enum names (which are in UPPER_SNAKE_CASE):
        return Enum.TryParse(upperSnakeCase, ignoreCase: false, out shortcut);
    }
};