using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The data of a compiled briefing, which its template binds to.
/// </summary>
/// <param name="Slots">The content of each slot, by its ID.</param>
/// <param name="Charts">The chart-library option object of each chart, by the ID of its component.</param>
/// <param name="Interactions">The controls, their state, and the formulas of the briefing.</param>
/// <param name="Accessibility">The text for people who cannot see a component, by the ID of the component.</param>
/// <param name="SourceReferences">The sources of each component, by the ID of the component.</param>
/// <param name="Labels">The fixed labels which the runtime of the briefing shows.</param>
internal sealed record VisualBriefingCompiledData(
    Dictionary<string, JsonElement> Slots,
    Dictionary<string, JsonElement> Charts,
    JsonElement Interactions,
    Dictionary<string, string> Accessibility,
    Dictionary<string, List<string>> SourceReferences,
    VisualBriefingLabels Labels);