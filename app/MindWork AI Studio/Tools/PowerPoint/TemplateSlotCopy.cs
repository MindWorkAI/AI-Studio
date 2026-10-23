namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// A shape of a template slide which repeats a slot of its layout, e.g., a second heading with its
/// text, which the author copied from the placeholders of the slide.
/// </summary>
/// <remarks>
/// PowerPoint turns a copied placeholder into a text box which keeps the look of the placeholder,
/// but no longer belongs to the layout. The slide still means it as one more slot of the same kind,
/// so the model fills or removes it like any slot, just as a table pasted onto a slide counts as the
/// table of the slide.
/// </remarks>
/// <param name="Slot">The slot the copy offers: the role of the slot it repeats, the next number of that role, and the place and capacity of the copy.</param>
/// <param name="ShapeId">The ID of the shape on the slide, which finds the copy again when the presentation is written.</param>
internal sealed record TemplateSlotCopy(TemplateSlot Slot, uint ShapeId);