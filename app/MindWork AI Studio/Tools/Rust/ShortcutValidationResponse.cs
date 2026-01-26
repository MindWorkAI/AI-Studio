namespace AIStudio.Tools.Rust;

public sealed record ShortcutValidationResponse(bool IsValid, string ErrorMessage, bool HasConflict, string ConflictDescription);