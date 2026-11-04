using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// The interactions in the data of a briefing: its controls, their state, and the formulas which depend on them.
/// </summary>
/// <param name="Controls">The validated interaction controls.</param>
/// <param name="State">The initial value of each control, by its ID.</param>
/// <param name="Formulas">The formula of each result slot, by the ID of the slot.</param>
internal sealed record VisualBriefingInteractionData(
    IReadOnlyList<VisualBriefingControlSpec> Controls,
    Dictionary<string, JsonElement> State,
    Dictionary<string, VisualBriefingFormulaNode> Formulas);