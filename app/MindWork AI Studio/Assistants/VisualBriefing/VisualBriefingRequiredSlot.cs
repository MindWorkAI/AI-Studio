namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// A slot which the content stage asks its model to fill, with what the slot is for.
/// </summary>
/// <param name="SlotId">The ID of the slot.</param>
/// <param name="Role">The semantic role of the slot.</param>
/// <param name="Type">The type of content the slot takes.</param>
internal sealed record VisualBriefingRequiredSlot(string SlotId, VisualBriefingSlotRole Role, VisualBriefingSlotType Type);