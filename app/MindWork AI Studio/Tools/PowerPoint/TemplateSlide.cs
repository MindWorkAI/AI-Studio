namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide a template carries, which the presentation may keep as it is or have the model edit.
/// </summary>
/// <remarks>
/// A template made for PowerPoint's "New" dialog carries samples or hints, which a presentation
/// leaves out. A presentation used as a template, though, often carries slides worth keeping: the
/// title slide, a picture opening each chapter, the closing slide. Which ones the presentation
/// keeps is up to the user; what a slide holds tells the model what it is about.
/// </remarks>
/// <param name="Number">The number of the slide in the template, starting at 1, as PowerPoint counts it; the model refers to the slide by it.</param>
/// <param name="PartUri">Where the slide is stored inside the file, which identifies it when the presentation is written.</param>
/// <param name="LayoutPartUri">Where the layout of the slide is stored; null when the slide names none.</param>
/// <param name="FamilyId">The ID of the layout family the slide is based on; null when its layout belongs to none, e.g., because it has no room for content.</param>
/// <param name="Title">The title of the slide on a single line; empty when it has none.</param>
/// <param name="Contents">What the slots of the slide hold, in the reading order of its layout, a table which is no placeholder included. Slots left empty are missing.</param>
/// <param name="IsHidden">Whether the slide is hidden in the slide show.</param>
/// <param name="IsImprint">Whether the slide is an imprint, which takes the place of the one every presentation gets otherwise.</param>
internal sealed record TemplateSlide(
    int Number,
    Uri PartUri,
    Uri? LayoutPartUri,
    string? FamilyId,
    string Title,
    IReadOnlyList<TemplateSlideContent> Contents,
    bool IsHidden,
    bool IsImprint);