namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide layout of a template.
/// </summary>
/// <param name="Name">The name the template gives the layout.</param>
/// <param name="PartUri">Where the layout is stored inside the file, which identifies it when the presentation is written.</param>
/// <param name="LayoutType">The layout type as the file spells it, e.g., "title" or "secHead"; empty when the file states none.</param>
/// <param name="Slots">The slots in reading order: from top to bottom, and from left to right within a row.</param>
internal sealed record TemplateLayout(string Name, Uri PartUri, string LayoutType, IReadOnlyList<TemplateSlot> Slots)
{
    /// <summary>
    /// Finds the slot a placeholder of a slide based on this layout fills.
    /// </summary>
    /// <remarks>
    /// PowerPoint links the placeholders of a slide to those of its layout by their index, and a
    /// placeholder without an index, such as the title, by its type. A slide often states the type
    /// less precisely than its layout does, e.g., none at all for a body, so the index wins.
    /// </remarks>
    /// <param name="placeholderType">The placeholder type as the slide spells it; "obj" when it states none.</param>
    /// <param name="placeholderIndex">The placeholder index the slide states, if any.</param>
    /// <returns>The slot, or null when the placeholder fills none, e.g., because it is the footer.</returns>
    public TemplateSlot? SlotOf(string placeholderType, uint? placeholderIndex)
    {
        if (placeholderIndex is { } index && this.Slots.FirstOrDefault(slot => slot.PlaceholderIndex == index) is { } slotByIndex)
            return slotByIndex;

        var typeGroup = TypeGroupOf(placeholderType);
        return this.Slots.FirstOrDefault(slot => (slot.PlaceholderIndex is null || placeholderIndex is null) && TypeGroupOf(slot.PlaceholderType) == typeGroup);
    }

    /// <summary>
    /// The type a placeholder is matched by: a title is a title, whether it is centered or not.
    /// </summary>
    private static string TypeGroupOf(string placeholderType) => placeholderType == "ctrTitle" ? "title" : placeholderType;
}