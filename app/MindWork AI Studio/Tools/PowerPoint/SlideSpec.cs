namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide as the model describes it: a layout family of the template, and the content of its slots.
/// </summary>
/// <remarks>
/// The lists fill the slots of their role in reading order: the first text goes into "text 1" of the
/// catalog, the second into "text 2", and so on. An empty entry keeps its slot empty, and the
/// entries after it keep their slots. A slot left without content is removed from the slide, so
/// that no prompt text of the template stays behind.
/// </remarks>
/// <param name="Layout">The ID of the layout family, e.g., "L3".</param>
/// <param name="StartsChapter">Whether a new chapter starts with this slide, which may change the color variant.</param>
/// <param name="Title">The title; empty when the slide has none.</param>
/// <param name="Subtitle">The subtitle; empty when the slide has none.</param>
/// <param name="Texts">The content of the text slots.</param>
/// <param name="Labels">The content of the label slots.</param>
/// <param name="Pictures">The pictures for the picture slots, as 1-based numbers of the attached pictures.</param>
/// <param name="Table">The content of the table slot, if any.</param>
internal sealed record SlideSpec(
    string Layout,
    bool StartsChapter,
    string Title,
    string Subtitle,
    IReadOnlyList<SlideTextBlock> Texts,
    IReadOnlyList<string> Labels,
    IReadOnlyList<int> Pictures,
    SlideTableSpec? Table);