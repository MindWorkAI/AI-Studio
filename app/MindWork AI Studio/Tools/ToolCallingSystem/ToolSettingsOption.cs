namespace AIStudio.Tools.ToolCallingSystem;

/// <param name="Value">The value stored and sent to the service.</param>
/// <param name="Label">What the user reads in the dropdown.</param>
public sealed record ToolSettingsOption(string Value, string Label);