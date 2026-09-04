using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

public sealed record AnthropicToolUse
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public JsonElement Input { get; init; }

    /// <summary>
    /// The arguments as JSON text, which is what the tool executor works with.
    /// </summary>
    public string Arguments => this.Input.ValueKind is JsonValueKind.Undefined ? "{}" : this.Input.GetRawText();
}