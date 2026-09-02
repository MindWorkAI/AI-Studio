using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>
/// One turn of the model, handed back unchanged so the conversation can continue.
/// </summary>
/// <remarks>
/// The content blocks are kept as raw JSON on purpose. A turn can carry text, tool uses, and
/// thinking blocks, and the thinking blocks have to return exactly as they arrived — reading and
/// rebuilding them would risk changing them.
/// </remarks>
public sealed record AnthropicMessage(IList<JsonElement> Content, string Role = "assistant") : IMessage<IList<JsonElement>>;

/// <summary>
/// The results of the tools the model asked for, as one user turn.
/// </summary>
/// <remarks>
/// All results of one turn belong in a single message. Splitting them across several messages
/// teaches the model to stop asking for more than one tool at a time.
/// </remarks>
public sealed record AnthropicToolResultMessage(IList<AnthropicToolResultContent> Content, string Role = "user") : IMessage<IList<AnthropicToolResultContent>>;

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

/// <summary>
/// One non-streamed answer of the Anthropic messages API.
/// </summary>
public sealed record AnthropicResponse
{
    public string StopReason { get; init; } = string.Empty;

    public IList<JsonElement> Content { get; init; } = [];

    /// <summary>
    /// The tool calls the model asked for.
    /// </summary>
    /// <remarks>
    /// A block without an ID or a name cannot be answered and is dropped here, so the harness
    /// sees a well-formed list. Anthropic supplies both for every real tool use.
    /// </remarks>
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

    /// <summary>
    /// The text the model wrote, with its blocks joined.
    /// </summary>
    public string GetTextOutput() => string.Concat(this.Content
        .Where(x => ReadString(x, "type").Equals("text", StringComparison.Ordinal))
        .Select(x => ReadString(x, "text")));

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

    /// <summary>
    /// The arguments as JSON text, which is what the tool executor works with.
    /// </summary>
    public string Arguments => this.Input.ValueKind is JsonValueKind.Undefined
        ? "{}"
        : this.Input.GetRawText();
}