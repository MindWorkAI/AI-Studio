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

    /// <summary>
    /// The human-readable thinking the model returned, with redacted blocks omitted.
    /// </summary>
    /// <remarks>
    /// Each thinking block reads as its own paragraph, so they are joined as paragraphs
    /// rather than run into one another.
    /// </remarks>
    public string GetThinkingOutput() => string.Join(
        $"{Environment.NewLine}{Environment.NewLine}",
        this.Content
            .Where(x => ReadString(x, "type").Equals("thinking", StringComparison.Ordinal))
            .Select(x => ReadString(x, "thinking"))
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string ReadString(JsonElement item, string propertyName)
    {
        if (item.ValueKind is not JsonValueKind.Object ||
            !item.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }
}
