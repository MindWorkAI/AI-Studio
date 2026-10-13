namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Layouts which offer the same slots at the same places and differ only in their look.
/// </summary>
/// <remarks>
/// Corporate templates often carry every layout in several colors. The model chooses what goes on a
/// slide, never its color: it sees one entry per family, and the color is chosen for the whole
/// presentation or per chapter. Since all variants share the slots, slot n of one variant is slot n
/// of every other one.
/// </remarks>
/// <param name="Id">The short ID the model uses to name the family, e.g., "L3".</param>
/// <param name="Name">The name the layouts share, e.g., "Inhalt/Content 1".</param>
/// <param name="Variants">The variants, in the order of the template; never empty.</param>
internal sealed record LayoutFamily(string Id, string Name, IReadOnlyList<LayoutVariant> Variants)
{
    /// <summary>
    /// The slots every variant of the family offers.
    /// </summary>
    public IReadOnlyList<TemplateSlot> Slots => this.Variants[0].Layout.Slots;

    /// <summary>
    /// The layout of the given variant, or the first one when the family has no such variant.
    /// </summary>
    /// <param name="variantLabel">The label of the variant, e.g., "B".</param>
    public TemplateLayout GetLayout(string? variantLabel) => (this.Variants.FirstOrDefault(variant => variant.Label == variantLabel) ?? this.Variants[0]).Layout;
}