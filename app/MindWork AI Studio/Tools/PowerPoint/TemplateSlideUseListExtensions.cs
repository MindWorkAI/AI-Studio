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
}