using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

public sealed record AnthropicTool
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool Strict { get; init; }

    public JsonElement InputSchema { get; init; }
}

public sealed record AnthropicMessage(IList<JsonElement> Content, string Role) : IMessage<IList<JsonElement>>;

public sealed record AnthropicToolResultMessage(IList<AnthropicToolResultContent> Content, string Role = "user") : IMessage<IList<AnthropicToolResultContent>>;

public sealed record AnthropicToolResultContent
{
    public string Type { get; init; } = "tool_result";

    public string ToolUseId { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}

public sealed record AnthropicResponse
{
    public string StopReason { get; init; } = string.Empty;

    public IList<JsonElement> Content { get; init; } = [];

    public IReadOnlyList<AnthropicToolUse> GetToolUses() => this.Content
        .Where(x => ReadString(x, "type").Equals("tool_use", StringComparison.Ordinal))
        .Select(x => new AnthropicToolUse
        {
            Id = ReadString(x, "id"),
            Name = ReadString(x, "name"),
            Input = x.TryGetProperty("input", out var input) ? input : default,
        })
        .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name))
        .ToList();

    public string GetTextOutput() => string.Concat(this.Content
        .Where(x => ReadString(x, "type").Equals("text", StringComparison.Ordinal))
        .Select(x => ReadString(x, "text")));

    public bool HasFinalStopReason() => this.StopReason is "" or "end_turn" or "stop_sequence";

    private static string ReadString(JsonElement item, string propertyName)
    {
        if (item.ValueKind is not JsonValueKind.Object ||
            !item.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }
}

public sealed record AnthropicToolUse
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public JsonElement Input { get; init; }

    public string Arguments => this.Input.ValueKind is JsonValueKind.Undefined
        ? "{}"
        : this.Input.GetRawText();
}
