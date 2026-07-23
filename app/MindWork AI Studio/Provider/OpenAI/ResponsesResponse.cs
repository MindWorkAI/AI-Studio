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

    public string GetTextOutput()
    {
        if (!string.IsNullOrWhiteSpace(this.OutputText))
            return this.OutputText;

        return string.Concat(this.Output
            .Where(x => ReadString(x, "type").Equals("message", StringComparison.Ordinal))
            .SelectMany(ReadContentItems)
            .Select(x => ReadString(x, "type") switch
            {
                "output_text" => ReadString(x, "text"),
                "refusal" => ReadString(x, "refusal"),
                _ => string.Empty,
            }));
    }

    public IReadOnlyList<Source> GetSources() => this.Output
        .Where(x => ReadString(x, "type").Equals("message", StringComparison.Ordinal))
        .SelectMany(ReadContentItems)
        .SelectMany(x => ReadArrayItems(x, "annotations"))
        .Where(x => ReadString(x, "type").Equals("url_citation", StringComparison.Ordinal))
        .Select(x => new Source(ReadString(x, "title"), ReadString(x, "url"), SourceOrigin.LLM))
        .Where(x => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.URL))
        .ToList();

    private static IEnumerable<JsonElement> ReadContentItems(JsonElement outputItem)
        => ReadArrayItems(outputItem, "content");

    private static IEnumerable<JsonElement> ReadArrayItems(JsonElement item, string propertyName)
    {
        if (item.ValueKind is not JsonValueKind.Object ||
            !item.TryGetProperty(propertyName, out var array) ||
            array.ValueKind is not JsonValueKind.Array)
            yield break;

        foreach (var arrayItem in array.EnumerateArray())
            yield return arrayItem;
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
