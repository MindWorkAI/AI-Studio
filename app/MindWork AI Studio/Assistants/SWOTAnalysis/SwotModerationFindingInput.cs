using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// A preliminary finding, as the moderator gets it.
/// </summary>
/// <param name="FactorId">The ID of the finding, for reference only: the moderator numbers the final findings anew.</param>
/// <param name="Summary">The summary of the finding.</param>
/// <param name="Explanation">The explanation of the finding.</param>
internal sealed record SwotModerationFindingInput(
    [property: JsonPropertyName("factor_id")] string FactorId,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("explanation")] string Explanation);