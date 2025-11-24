namespace AIStudio.Tools.Rust;

/// <summary>
/// The data structure for a Tauri event sent from the Rust backend to the C# frontend.
/// </summary>
/// <param name="EventType">The type of the Tauri event.</param>
/// <param name="Payload">The payload of the Tauri event.</param>
public readonly record struct TauriEvent(TauriEventType EventType, List<string> Payload);