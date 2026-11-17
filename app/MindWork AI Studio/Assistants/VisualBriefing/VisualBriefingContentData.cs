using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The data of a content artifact, as the content stage keeps it along with the artifact.
/// </summary>
/// <param name="Slots">The content of each slot, by its ID.</param>
/// <param name="Charts">The validated chart specifications.</param>
/// <param name="Controls">The validated interaction controls.</param>
/// <param name="Formulas">The validated formula specifications.</param>
/// <param name="Accessibility">The text for people who cannot see a component, by the ID of the component.</param>
/// <param name="SourceReferences">The sources of each component, by the ID of the component.</param>
/// <param name="Labels">The fixed labels which the runtime of the briefing shows.</param>
internal sealed record VisualBriefingContentData(
    Dictionary<string, JsonElement> Slots,
    List<VisualBriefingChartSpec> Charts,
    List<VisualBriefingControlSpec> Controls,
    List<VisualBriefingFormulaSpec> Formulas,
    Dictionary<string, string> Accessibility,
    Dictionary<string, List<string>> SourceReferences,
    VisualBriefingLabels Labels)
{
    /// <summary>
    /// Gathers the data of a content artifact.
    /// </summary>
    /// <param name="artifact">The content artifact.</param>
    /// <returns>The data of the artifact.</returns>
    internal static VisualBriefingContentData Of(VisualBriefingContentArtifact artifact) => new(
        artifact.Slots.ToDictionary(slot => slot.SlotId, slot => slot.Value, StringComparer.Ordinal),
        artifact.Charts,
        artifact.Controls,
        artifact.Formulas,
        artifact.AccessibilityTexts,
        artifact.SourceReferences,
        VisualBriefingLabels.Of(artifact.ResetLabel));
}