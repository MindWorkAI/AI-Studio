namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What one slot of a template slide holds.
/// </summary>
/// <param name="Slot">The slot, as the layout of the slide offers it.</param>
/// <param name="Text">The text, one paragraph per line; empty for a picture. A table gives one row per line, with " | " between its cells.</param>
internal sealed record TemplateSlideContent(TemplateSlot Slot, string Text);