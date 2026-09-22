namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSettingsSchema
{
    public string Type { get; init; } = "object";

    public Dictionary<string, ToolSettingsFieldDefinition> Properties { get; init; } = [];

    public HashSet<string> Required { get; init; } = [];
}