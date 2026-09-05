using System.Text.Json;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolFunctionDefinition
{
    public string Name { get; init; } = string.Empty;

    public string DescriptionForLLM { get; init; } = string.Empty;

    public bool Strict { get; init; } = true;

    public JsonElement Parameters { get; init; }
}