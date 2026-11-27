namespace AIStudio.Components;

/// <summary>
/// Settings every text field of AI Studio shares.
/// </summary>
public static class TextFieldDefaults
{
    /// <summary>
    /// The time in milliseconds after the last keystroke when a text field with a validation checks its text again.
    /// </summary>
    /// <remarks>
    /// Without Immediate or a DebounceInterval, a MudTextField passes its text on only when the user leaves it, so an
    /// issue stays visible while the user types the text which resolves it. A text field with a validation therefore
    /// sets DebounceInterval to this value and binds its Value, not its Text: bound by its text, the field would delay
    /// the texts its parent sets as well, e.g., by a reset, and check them without having been touched.
    /// </remarks>
    public const double VALIDATION_DEBOUNCE_MILLISECONDS = 500;
}