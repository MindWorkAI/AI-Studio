using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Non-streaming OpenAI Responses API result used during local tool execution.
/// </summary>
public sealed record ResponsesResponse
{
    public string Id { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? OutputText { get; init; }

    public IList<JsonElement> Output { get; init; } = [];

    public IReadOnlyList<ResponsesFunctionCallItem> GetFunctionCalls() => this.Output
        .Where(x => ReadString(x, "type").Equals("function_call", StringComparison.Ordinal))
        .Select(x => new ResponsesFunctionCallItem
        {
            Type = ReadString(x, "type"),
            CallId = ReadString(x, "call_id"),
            Name = ReadString(x, "name"),
            Arguments = ReadString(x, "arguments"),
        })
        .Where(x => !string.IsNullOrWhiteSpace(x.CallId) && !string.IsNullOrWhiteSpace(x.Name))
        .ToList();

    public IReadOnlyList<JsonElement> GetRawFunctionCallItems() => this.Output
        .Where(x => ReadString(x, "type").Equals("function_call", StringComparison.Ordinal))
        .ToList();

    public string GetTextOutput()
    {
        if (!string.IsNullOrWhiteSpace(this.OutputText))
            return this.OutputText;

        return string.Concat(this.Output
            .Where(x => ReadString(x, "type").Equals("message", StringComparison.Ordinal))
            .SelectMany(ReadContentItems)
            .Where(x => ReadString(x, "type").Equals("output_text", StringComparison.Ordinal))
            .Select(x => ReadString(x, "text")));
    }

    private static IEnumerable<JsonElement> ReadContentItems(JsonElement outputItem)
    {
        if (outputItem.ValueKind is not JsonValueKind.Object ||
            !outputItem.TryGetProperty("content", out var content) ||
            content.ValueKind is not JsonValueKind.Array)
            yield break;

        foreach (var contentItem in content.EnumerateArray())
            yield return contentItem;
    }

    private static string ReadString(JsonElement item, string propertyName)
    {
        if (item.ValueKind is not JsonValueKind.Object ||
            !item.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }
}
