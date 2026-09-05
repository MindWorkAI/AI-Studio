using System.Text.Json.Serialization;

namespace AIStudio.Provider.Anthropic;

public sealed record AnthropicToolResultContent
{
    public string Type { get; init; } = "tool_result";

    public string ToolUseId { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Whether the tool failed rather than returning a result.
    /// </summary>
    /// <remarks>
    /// Only sent when true: Anthropic reads its absence as success, and this way a successful
    /// result stays byte-identical to what earlier versions sent.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsError { get; init; }
}