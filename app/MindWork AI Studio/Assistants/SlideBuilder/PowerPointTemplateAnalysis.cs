namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Identifies the compatible layouts selected from a PowerPoint template.
/// </summary>
/// <param name="TitleLayoutUri">The package URI of the selected title layout.</param>
/// <param name="ContentLayoutUri">The package URI of the selected title-and-content layout.</param>
internal sealed record PowerPointTemplateAnalysis(
    string TitleLayoutUri,
    string ContentLayoutUri);
