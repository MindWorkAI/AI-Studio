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
    TextCapacity? Capacity)
{
    /// <summary>
    /// The slot where a slide moved or resized its placeholder.
    /// </summary>
    /// <remarks>
    /// The capacity grows and shrinks with the size, since the slide keeps the font. Insets stay as
    /// they are on a smaller slot, so the estimate runs a little high there; the check of the
    /// model's answer accepts a line more anyway.
    /// </remarks>
    /// <param name="bounds">Where the slide puts the placeholder.</param>
    public TemplateSlot MovedTo(SlotBounds bounds) => this with
    {
        Bounds = bounds,
        Capacity = this.Capacity is { } capacity
            ? new TextCapacity(
                Math.Max(1, (int)(capacity.Lines * (double)bounds.Height / Math.Max(1, this.Bounds.Height))),
                Math.Max(1, (int)(capacity.CharactersPerLine * (double)bounds.Width / Math.Max(1, this.Bounds.Width))))
            : null,
    };
}