namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What the user reads about an area of kinds of slides.
/// </summary>
internal static class LayoutPurposeAreaExtensions
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(LayoutPurposeAreaExtensions).Namespace, nameof(LayoutPurposeAreaExtensions));

    /// <summary>
    /// The name of the area, as the forms show it above its kinds of slides.
    /// </summary>
    /// <param name="area">The area.</param>
    public static string Label(this LayoutPurposeArea area) => area switch
    {
        LayoutPurposeArea.FRAMING => TB("Structure and framing"),
        LayoutPurposeArea.GENERAL => TB("General content"),
        LayoutPurposeArea.TEACHING => TB("Teaching and workshops"),
        LayoutPurposeArea.BUSINESS => TB("Business and projects"),
        LayoutPurposeArea.SCIENCE => TB("Science and research"),
        LayoutPurposeArea.MEDICINE => TB("Medicine"),
        LayoutPurposeArea.LAW => TB("Law"),

        _ => TB("Other kinds of slides"),
    };
}