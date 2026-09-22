using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// The body of one Staan search request.
/// </summary>
/// <remarks>
/// Only what this tool sends is declared. Staan also takes lists of domains to include or
/// exclude, which this tool has no argument for, and the number of results per page is fixed
/// at ten regardless of what a request asks for.
/// </remarks>
internal sealed record StaanSearchRequest
{
    /// <summary>
    /// What to search for. Staan rejects a query longer than 400 characters.
    /// </summary>
    [JsonPropertyName("q")]
    public required string Query { get; init; }

    /// <summary>
    /// The market to search in. Staan offers de-de, en-us, and fr-fr, and defaults to fr-fr.
    /// </summary>
    public string? Market { get; init; }

    /// <summary>
    /// Where in the result list to start, in steps of ten up to 30, or null for the first page.
    /// </summary>
    public int? Offset { get; init; }
}