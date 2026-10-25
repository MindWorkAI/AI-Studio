namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One variant of a layout family, e.g., the green version of a content layout.
/// </summary>
/// <param name="Label">The part of the layout name which tells the variants apart, e.g., "B"; null for a family of a single layout.</param>
/// <param name="Layout">The layout.</param>
internal sealed record LayoutVariant(string? Label, TemplateLayout Layout);