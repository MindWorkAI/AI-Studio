namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide layout of a template.
/// </summary>
/// <param name="Name">The name the template gives the layout.</param>
/// <param name="PartUri">Where the layout is stored inside the file, which identifies it when the presentation is written.</param>
/// <param name="LayoutType">The layout type as the file spells it, e.g., "title" or "secHead"; empty when the file states none.</param>
/// <param name="Slots">The slots in reading order: from top to bottom, and from left to right within a row.</param>
internal sealed record TemplateLayout(
    string Name,
    Uri PartUri,
    string LayoutType,
    IReadOnlyList<TemplateSlot> Slots);