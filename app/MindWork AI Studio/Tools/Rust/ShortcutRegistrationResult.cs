namespace AIStudio.Tools.Rust;

/// <summary>
/// Typed result of a global shortcut registration attempt.
/// </summary>
public sealed record ShortcutRegistrationResult(
    bool Success,
    string ErrorMessage,
    ShortcutBackend Backend,
    bool Cancelled,
    string EffectiveDisplayName)
{
    public static ShortcutRegistrationResult Failed(string errorMessage) =>
        new(false, errorMessage, ShortcutBackend.NONE, false, string.Empty);
}
