namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// A component for which the content stage asks its model to write controls and formulas.
/// </summary>
/// <param name="ComponentId">The ID of the component.</param>
/// <param name="Kind">The kind of the component.</param>
/// <param name="PanelSlotIds">The IDs of the panel slots of the component, in their order.</param>
/// <param name="ResultSlotIds">The IDs of the result slots of the component, in their order.</param>
internal sealed record VisualBriefingControlRequirement(string ComponentId, VisualBriefingComponentKind Kind, string[] PanelSlotIds, string[] ResultSlotIds);