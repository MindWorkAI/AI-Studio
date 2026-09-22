using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// A tool as the Anthropic messages API expects it.
/// </summary>
/// <remarks>
/// Anthropic names the schema field input schema, where the chat completions and responses APIs
/// call it parameters. The description is the plain one, without their nesting.
/// </remarks>
public sealed record AnthropicTool
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Strict { get; init; }

    public JsonElement InputSchema { get; init; }
}