namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One slide as the model describes it: a layout family of the template, and the content of its slots.
/// </summary>
/// <remarks>
/// The lists fill the slots of their role in reading order: the first text goes into "text 1" of the
/// catalog, the second into "text 2", and so on. An empty entry keeps its slot empty, and the
/// entries after it keep their slots. A slot left without content is removed from the slide, so
/// that no prompt text of the template stays behind.
///
/// A slide may refer to a slide of the template instead of a layout. Without content, it keeps that
/// slide as it is. With content, the content replaces what the slots of that slide hold, and every
/// slot it leaves empty keeps the content of the template, unless the slide removes it. What the
/// template slide carries besides its slots stays as well, unless the slide removes it. Where the
/// template slide carries copies of a slot, e.g., a second heading with its text, the numbers go on
/// through them: "label 2" is the first copy of a layout with a single label.
/// </remarks>
/// <param name="Layout">The ID of the layout family, e.g., "L3"; not used when the slide refers to a template slide, which keeps its own layout.</param>
/// <param name="StartsChapter">Whether a new chapter starts with this slide, which opens a section of the presentation.</param>
/// <param name="Title">The title; empty when the slide has none.</param>
/// <param name="Subtitle">The subtitle; empty when the slide has none.</param>
/// <param name="Texts">The content of the text slots.</param>
/// <param name="Labels">The content of the label slots.</param>
/// <param name="Pictures">The pictures for the picture slots, as 1-based numbers of the attached pictures.</param>
/// <param name="Table">The content of the table slot, if any.</param>
/// <param name="TemplateSlide">The number of the template slide the slide keeps or edits; null for a new slide.</param>
internal sealed record SlideSpec(string Layout, bool StartsChapter, string Title, string Subtitle, IReadOnlyList<SlideTextBlock> Texts, IReadOnlyList<string> Labels, IReadOnlyList<int> Pictures, SlideTableSpec? Table, int? TemplateSlide = null)
{
    /// <summary>
    /// What the slide removes from the template slide it edits, by the names the model read: slots,
    /// e.g., "label 1", and elements, e.g., "element 2". Empty for every other slide.
    /// </summary>
    public IReadOnlyList<string> Removals { get; init; } = [];

    /// <summary>
    /// Whether the slide brings content of its own, rather than keeping a template slide as it is.
    /// </summary>
    /// <remarks>
    /// Removing something changes a template slide as well.
    /// </remarks>
    public bool HasContent =>
        this.Title.Length > 0 ||
        this.Subtitle.Length > 0 ||
        this.Texts.Any(text => text.Paragraphs.Count > 0) ||
        this.Labels.Any(label => label.Length > 0) ||
        this.Pictures.Count > 0 ||
        this.Table is not null ||
        this.Removals.Count > 0;

    /// <summary>
    /// Whether the slide has content for a slot.
    /// </summary>
    /// <param name="role">The role of the slot.</param>
    /// <param name="ordinal">The number of the slot among the slots of its role, starting at 1.</param>
    public bool Fills(PlaceholderRole role, int ordinal) => role switch
    {
        PlaceholderRole.TITLE => this.Title.Length > 0,
        PlaceholderRole.SUBTITLE => this.Subtitle.Length > 0,
        PlaceholderRole.TEXT => ordinal >= 1 && ordinal <= this.Texts.Count && this.Texts[ordinal - 1].Paragraphs.Count > 0,
        PlaceholderRole.LABEL => ordinal >= 1 && ordinal <= this.Labels.Count && this.Labels[ordinal - 1].Length > 0,
        PlaceholderRole.PICTURE => ordinal >= 1 && ordinal <= this.Pictures.Count,
        PlaceholderRole.TABLE => ordinal == 1 && this.Table is { Headers.Count: > 0 },

        _ => false,
    };

    /// <summary>
    /// Whether the slide removes the content a slot of its template slide holds.
    /// </summary>
    /// <param name="slot">The slot.</param>
    public bool Removes(TemplateSlot slot) => this.Removals.Any(name => PlaceholderRoleExtensions.TryParseSlotName(name, out var role, out var ordinal) && role == slot.Role && ordinal == slot.Ordinal);
}