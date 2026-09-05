namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

/// <summary>
/// The body of one Tavily search request.
/// </summary>
/// <remarks>
/// Only what this tool sends is declared. Tavily can also return an answer written by a model
/// and the raw content of every hit, both of which cost extra credits and would bypass this
/// tool's own page reader and its prompt injection filtering.
/// </remarks>
internal sealed record TavilySearchRequest
{
    public required string Query { get; init; }

    /// <summary>
    /// How thoroughly to search. A basic search costs one credit, an advanced one costs two.
    /// </summary>
    public required string SearchDepth { get; init; }

    /// <summary>
    /// The most hits to return, at most 20.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// How far back to look: day, week, month, or year, or null for no restriction.
    /// </summary>
    public string? TimeRange { get; init; }

    /// <summary>
    /// The language to search in, as an ISO 639-1 code, or null for no restriction.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Whether the language is a requirement rather than a preference. Needs the language field.
    /// </summary>
    public bool? FilterByLanguage { get; init; }

    /// <summary>
    /// Whether to filter explicit results or null to leave the decision to Tavily.
    /// </summary>
    public bool? SafeSearch { get; init; }
}