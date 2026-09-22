namespace AIStudio.Tools.Rust;

/// <summary>
/// The data structure for a Tauri event sent from the Rust backend to the C# frontend.
/// </summary>
/// <param name="EventType">The type of the Tauri event.</param>
/// <param name="Payload">The payload of the Tauri event.</param>
/// <param name="Position">Where the cursor was, for the drag and drop events which know it.</param>
public readonly record struct TauriEvent(TauriEventType EventType, List<string> Payload, DropPosition? Position = null)
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
    /// Reads the cursor position of a drag and drop event.
    /// </summary>
    /// <remarks>
    /// The coordinates are viewport-relative CSS pixels, ready for a hit test in the browser. Only the
    /// drag and drop events carry them, which is why the caller has to ask instead of assuming.
    /// </remarks>
    /// <param name="x">The distance from the left edge of the viewport, in CSS pixels.</param>
    /// <param name="y">The distance from the top edge of the viewport, in CSS pixels.</param>
    /// <returns>True if the event carried a position, false otherwise.</returns>
    public bool TryGetDropPosition(out double x, out double y)
    {
        x = 0.0;
        y = 0.0;
        if (this.Position is not { } position)
            return false;

        x = position.X;
        y = position.Y;
        return true;
    }

    /// <summary>
    /// Reads a portal shortcut change and its effective display name.
    /// </summary>
    public bool TryGetShortcutChange(out Shortcut shortcut, out string effectiveDisplayName)
    {
        shortcut = default;
        effectiveDisplayName = string.Empty;
        if (this.EventType != TauriEventType.GLOBAL_SHORTCUT_CHANGED || this.Payload.Count < 2)
            return false;

        if (!Enum.TryParse(this.Payload[0], ignoreCase: true, out shortcut)
            && !TryParseSnakeCase(this.Payload[0], out shortcut))
            return false;

        effectiveDisplayName = this.Payload[1];
        return true;
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
