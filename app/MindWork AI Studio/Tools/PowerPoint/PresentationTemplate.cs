namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What a PowerPoint template offers to build slides from: its layouts, grouped into families.
/// </summary>
/// <remarks>
/// Read once from the template the user chose. The model gets the families as a catalog and picks
/// one for every slide; the presentation is then written from the layouts of the same template. The
/// corporate design therefore comes from the template alone, and nothing about any particular
/// template is written into the code.
/// </remarks>
internal sealed partial class PresentationTemplate
{
    private PresentationTemplate(long slideWidth, long slideHeight, IReadOnlyList<LayoutFamily> families, IReadOnlyList<string> variantLabels, IReadOnlyList<TemplateSlide> slides)
    {
        this.SlideWidth = slideWidth;
        this.SlideHeight = slideHeight;
        this.Families = families;
        this.VariantLabels = variantLabels;
        this.Slides = slides;
    }

    /// <summary>
    /// The width of a slide, in EMU.
    /// </summary>
    public long SlideWidth { get; }

    /// <summary>
    /// The height of a slide, in EMU.
    /// </summary>
    public long SlideHeight { get; }

    /// <summary>
    /// The layout families which have room for content, in the order of the template.
    /// </summary>
    public IReadOnlyList<LayoutFamily> Families { get; }

    /// <summary>
    /// The labels which tell the variants of the families apart, e.g., "A", "B" and "C"; empty when
    /// the template carries every layout once.
    /// </summary>
    public IReadOnlyList<string> VariantLabels { get; }

    /// <summary>
    /// The slides the template carries, in the order of the file; empty for a template without slides.
    /// </summary>
    public IReadOnlyList<TemplateSlide> Slides { get; }

    /// <summary>
    /// Finds a family by the ID the model used for it.
    /// </summary>
    /// <param name="id">The ID, e.g., "L3".</param>
    /// <returns>The family, or null when there is none with this ID.</returns>
    public LayoutFamily? FindFamily(string id) => this.Families.FirstOrDefault(family => string.Equals(family.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds a slide of the template by its number.
    /// </summary>
    /// <param name="number">The number, starting at 1.</param>
    /// <returns>The slide, or null when the template has no slide with this number.</returns>
    public TemplateSlide? FindSlide(int number) => number >= 1 && number <= this.Slides.Count ? this.Slides[number - 1] : null;

    /// <summary>
    /// Finds the family a slide of a deck is written on: that of the template slide it refers to, or the one it names.
    /// </summary>
    /// <param name="slide">The slide of the deck.</param>
    /// <returns>The family, or null when there is none, e.g., because the template slide is on a layout without room for content.</returns>
    public LayoutFamily? FamilyOf(SlideSpec slide) => slide.TemplateSlide is { } number
        ? this.FindSlide(number)?.FamilyId is { } familyId ? this.FindFamily(familyId) : null
        : this.FindFamily(slide.Layout);
}