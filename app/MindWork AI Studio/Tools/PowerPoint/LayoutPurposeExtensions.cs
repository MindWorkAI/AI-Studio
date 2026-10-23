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

    private static LayoutPurposeInfo Describe(LayoutPurpose purpose) =>
        DescribeFraming(purpose)
        ?? DescribeGeneral(purpose)
        ?? DescribeTeaching(purpose)
        ?? DescribeBusiness(purpose)
        ?? DescribeScience(purpose)
        ?? throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "There is no description for this kind of slide.");
}