using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The preliminary result of one category, as the moderator gets it.
/// </summary>
/// <param name="EmptyMessage">Why the category has no findings, or an empty string.</param>
/// <param name="Findings">The findings of the category, numbered for reference.</param>
internal sealed record SwotModerationCategoryInput(
    [property: JsonPropertyName("empty_message")] string EmptyMessage,
    [property: JsonPropertyName("findings")] IReadOnlyList<SwotModerationFindingInput> Findings);