using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Non-streaming OpenAI Responses API result used during local tool execution.
/// </summary>
public sealed record ResponsesResponse
{
    /// <summary>
    /// The output items after which the usage still describes the request as it was sent.
    /// </summary>
    /// <remarks>
    /// A list of the items which are known to leave the input alone, rather than one of those which
    /// do not: a hosted tool OpenAI adds later would otherwise inflate the number without anybody
    /// noticing.
    /// </remarks>
    private static readonly HashSet<string> OUTPUT_ITEMS_LEAVING_THE_INPUT_ALONE = ["message", "reasoning", "function_call"];

    public string Id { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string? OutputText { get; init; }

    public IList<JsonElement> Output { get; init; } = [];

    /// <summary>
    /// What OpenAI says the response cost. Only the completed response carries it.
    /// </summary>
    public ResponsesUsage? Usage { get; init; }

    /// <summary>
    /// States what the request of this response carried, as far as it can be believed.
    /// </summary>
    /// <remarks>
    /// Only for a response which ran no hosted tool. When OpenAI runs one, such as its web search,
    /// what the tool found is charged as input tokens of the same response, cf. the pricing read on
    /// 2026-09-24 at https://developers.openai.com/api/docs/pricing. No later request carries what
    /// the tool found, so the number would describe a conversation larger than the one there is --
    /// the same reason why the tool calling loop keeps only the usage of its first round.
    ///
    /// A response put back together from its items, for a gateway which never sent the completed
    /// event, has no usage. Nor is one read off a response which ended as incomplete, because it
    /// ran out of output tokens: that is rare enough for the estimate to cover it.
    /// </remarks>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the response states nothing usable.</returns>
    public TokenUsage GetUsage() => this.Output.All(x => OUTPUT_ITEMS_LEAVING_THE_INPUT_ALONE.Contains(ReadString(x, "type")))
        ? this.Usage?.ToTokenUsage() ?? TokenUsage.UNKNOWN
        : TokenUsage.UNKNOWN;

    public IReadOnlyList<ResponsesFunctionCallItem> GetFunctionCalls() => this.Output
        .Where(x => ReadString(x, "type").Equals("function_call", StringComparison.Ordinal))
        .Select(x => new ResponsesFunctionCallItem
        {
            Type = ReadString(x, "type"),
            CallId = ReadString(x, "call_id"),
            Name = ReadString(x, "name"),
            Arguments = ReadString(x, "arguments"),
        })
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
