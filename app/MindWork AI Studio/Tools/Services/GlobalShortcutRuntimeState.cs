using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed record GlobalShortcutRuntimeState(Shortcut ShortcutId, string Shortcut, ShortcutBackend Backend, bool IsSuspended);