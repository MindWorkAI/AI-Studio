namespace AIStudio.Tools.Rust;

public sealed record ShortcutResponse(
    bool Success,
    string ErrorMessage,
    ShortcutBackend Backend,
    bool Cancelled,
    string EffectiveDisplayName);
