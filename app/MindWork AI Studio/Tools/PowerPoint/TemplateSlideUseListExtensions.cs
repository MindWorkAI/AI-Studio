namespace AIStudio.Tools.PowerPoint;

internal static class TemplateSlideUseListExtensions
{
    /// <summary>
    /// What the user chose for one slide of the template.
    /// </summary>
    /// <param name="uses">What the user chose for each slide of the template, by its number.</param>
    /// <param name="number">The number of the slide, starting at 1.</param>
    /// <returns>The choice; TemplateSlideUse.OMIT for a slide the list does not cover.</returns>
    public static TemplateSlideUse UseOf(this IReadOnlyList<TemplateSlideUse> uses, int number) => number >= 1 && number <= uses.Count ? uses[number - 1] : TemplateSlideUse.OMIT;

    /// <summary>
    /// What the presentation does with each slide of a template, by the choices for some of its slides.
    /// </summary>
    /// <remarks>
    /// A slide without a choice is left out, as by default. A choice to edit a slide which the model
    /// cannot edit keeps the slide unchanged instead: such a choice may come from an organization,
    /// which the user cannot overrule, and it would block the form. A choice for a slide the template
    /// does not have is passed over.
    /// </remarks>
    /// <param name="choices">The choices by the number of the slide, starting at 1.</param>
    /// <param name="template">The template whose slides the choices number.</param>
    /// <returns>A choice for each slide of the template, in its order.</returns>
    public static List<TemplateSlideUse> ToTemplateSlideUses(this IReadOnlyDictionary<int, TemplateSlideUse> choices, PresentationTemplate template) => template.Slides
        .Select(slide => choices.TryGetValue(slide.Number, out var use) ? use is TemplateSlideUse.EDIT && !slide.IsEditable ? TemplateSlideUse.KEEP : use : TemplateSlideUse.OMIT)
        .ToList();
}