using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Icon { get; init; } = Icons.Material.Filled.Build;

    public string ImplementationKey { get; init; } = string.Empty;

    public ToolVisibilityDefinition VisibleIn { get; init; } = new();

    public ToolSettingsSchema SettingsSchema { get; init; } = new();

    public ToolFunctionDefinition Function { get; init; } = new();
}

public sealed class ToolVisibilityDefinition
{
    public bool Chat { get; init; } = true;

    public bool Assistants { get; init; } = true;
}

public sealed class ToolFunctionDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Strict { get; init; } = true;

    public JsonElement Parameters { get; init; }
}

public sealed class ToolSettingsSchema
{
    public string Type { get; init; } = "object";

    public Dictionary<string, ToolSettingsFieldDefinition> Properties { get; init; } = [];

    public HashSet<string> Required { get; init; } = [];
}

public sealed class ToolSettingsFieldDefinition
{
    public string Type { get; init; } = "string";

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("enum")]
    public List<string> EnumValues { get; init; } = [];

    public bool Secret { get; init; }
}
