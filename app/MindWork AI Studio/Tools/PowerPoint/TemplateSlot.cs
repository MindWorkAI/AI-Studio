namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One placeholder of a slide layout which the content of a slide goes into.
/// </summary>
/// <param name="Role">What the slot is for.</param>
/// <param name="Ordinal">The number of the slot among the slots of the same role, starting at 1, in reading order.</param>
/// <param name="Bounds">Where the slot sits, inherited from the slide master when the layout does not say.</param>
/// <param name="PlaceholderType">The placeholder type as the file spells it, e.g., "body" or "pic"; "obj" when the file states none.</param>
/// <param name="PlaceholderIndex">The placeholder index, which links a placeholder of a slide to the one of its layout.</param>
/// <param name="Capacity">How much text fits; missing for pictures.</param>
internal sealed record TemplateSlot(
    PlaceholderRole Role,
    int Ordinal,
    SlotBounds Bounds,
    string PlaceholderType,
    uint? PlaceholderIndex,
    TextCapacity? Capacity);