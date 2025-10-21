namespace AIStudio.Tools.Rust;

public readonly record struct TauriEvent(TauriEventType Type, List<string> Payload);