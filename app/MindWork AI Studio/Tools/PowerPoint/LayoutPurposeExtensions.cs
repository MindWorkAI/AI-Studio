namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the user and the model read about the kinds of slides the user may choose layouts for.
/// </summary>
/// <remarks>
/// Each area of kinds of slides describes its members in a file of its own, one line per kind, so
/// that everything about a kind stays in one place.
/// </remarks>
internal static partial class LayoutPurposeExtensions
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(LayoutPurposeExtensions).Namespace, nameof(LayoutPurposeExtensions));

    /// <summary>
    /// What the forms offer for a kind of slide the user leaves to the LLM.
    /// </summary>
    public static string NoPreference => TB("No preference (the LLM decides)");

    /// <summary>
    /// The area the forms list a kind of slide in.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static LayoutPurposeArea Area(this LayoutPurpose purpose) => Describe(purpose).Area;

    /// <summary>
    /// The short name of the choice, e.g., "Layout for chapters".
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static string Label(this LayoutPurpose purpose) => Describe(purpose).Label;

    /// <summary>
    /// The question the choice answers, which says which slides it applies to.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static string Question(this LayoutPurpose purpose) => Describe(purpose).Question;

    /// <summary>
    /// Describes the slides of this kind for the model, so that it knows which of its slides a choice applies to.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    /// <returns>The description, e.g., "timelines, e.g., the course of a project".</returns>
    public static string PromptDescription(this LayoutPurpose purpose) => Describe(purpose).PromptDescription;

    /// <summary>
    /// Whether choosing a layout for this kind of slide also puts such a slide into the presentation.
    /// </summary>
    /// <remarks>
    /// For most kinds, a choice says only which layout such a slide uses, and whether the
    /// presentation has one follows from its content. The slides which frame a presentation are
    /// different: whoever chooses a layout for the table of contents or the imprint expects them.
    /// </remarks>
    /// <param name="purpose">The kind of slide.</param>
    public static bool ChoiceAddsSlide(this LayoutPurpose purpose) => purpose.PromptPlacement() is not null;

    /// <summary>
    /// Where a slide goes which a choice puts into the presentation, and what it holds, for the model.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    /// <returns>The rule, or null when a choice adds no slide of this kind.</returns>
    public static string? PromptPlacement(this LayoutPurpose purpose) => purpose switch
    {
        LayoutPurpose.TABLE_OF_CONTENTS => "right after the title slide",
        LayoutPurpose.Q_AND_A => "near the end, before the closing slide; where the occasion suits it, e.g., in a lecture or a workshop, also at the end of chapters, then naming the topic of the chapter",
        LayoutPurpose.ACKNOWLEDGEMENTS => "near the end; where thanks concern a single chapter, also at the end of that chapter",
        LayoutPurpose.REFERENCES => "near the end; it lists only the sources the content names, or else the attached documents, and never a source of your own",
        LayoutPurpose.CLOSING => "at the end, before an appendix and the imprint",
        LayoutPurpose.IMPRINT => "as the very last slide; it holds only what the request, the content, or the profile state, e.g., the title, the authors, the organization, and today's date, and it leaves a slot empty rather than inventing anything",

        _ => null,
    };

    /// <summary>
    /// The help below the choice: its question, and whether a choice adds such a slide.
    /// </summary>
    /// <param name="purpose">The kind of slide.</param>
    public static string Help(this LayoutPurpose purpose) => purpose.ChoiceAddsSlide()
        ? $"{purpose.Question()} {TB("Choosing a layout also adds such a slide to the presentation.")}"
        : purpose.Question();

    private static LayoutPurposeInfo Describe(LayoutPurpose purpose) =>
        DescribeFraming(purpose)
        ?? DescribeGeneral(purpose)
        ?? DescribeTeaching(purpose)
        ?? DescribeBusiness(purpose)
        ?? DescribeScience(purpose)
        ?? throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "There is no description for this kind of slide.");
}