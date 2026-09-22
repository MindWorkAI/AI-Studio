using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// One non-streamed answer of the Anthropic messages API.
/// </summary>
public sealed record AnthropicResponse
{
    public string StopReason { get; init; } = string.Empty;

    public IList<JsonElement> Content { get; init; } = [];

    /// <summary>
    /// The argument text of those tool uses whose arguments never parsed, by tool use ID.
    /// </summary>
    /// <remarks>
    /// Empty for a non-streamed answer, where the arguments either arrived as an object or did
    /// not arrive at all.
    /// </remarks>
    public IReadOnlyDictionary<string, string> UnparsableToolInputs { get; init; } = new Dictionary<string, string>();

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
            UnparsableArguments = this.UnparsableToolInputs.GetValueOrDefault(ReadString(x, "id")),
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