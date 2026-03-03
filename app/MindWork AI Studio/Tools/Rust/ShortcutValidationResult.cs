namespace AIStudio.Tools.Rust;

/// <summary>
/// Result of validating a keyboard shortcut.
/// </summary>
/// <param name="IsValid">Whether the shortcut syntax is valid.</param>
/// <param name="ErrorMessage">Error message if not valid.</param>
/// <param name="HasConflict">Whether the shortcut conflicts with another registered shortcut.</param>
/// <param name="ConflictDescription">Description of the conflict, if any.</param>
public sealed record ShortcutValidationResult(bool IsValid, string ErrorMessage, bool HasConflict, string ConflictDescription);