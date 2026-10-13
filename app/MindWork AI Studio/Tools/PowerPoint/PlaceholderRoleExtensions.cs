namespace AIStudio.Tools.PowerPoint;

internal static class PlaceholderRoleExtensions
{
    /// <summary>
    /// Whether content of the presentation goes into a placeholder of this role.
    /// </summary>
    /// <remarks>
    /// Footer, slide number and date are filled as well, but by the presentation as a whole rather
    /// than by a single slide, so they are no slots of a layout.
    /// </remarks>
    public static bool IsSlot(this PlaceholderRole role) => role switch
    {
        PlaceholderRole.TITLE => true,
        PlaceholderRole.SUBTITLE => true,
        PlaceholderRole.TEXT => true,
        PlaceholderRole.LABEL => true,
        PlaceholderRole.PICTURE => true,
        PlaceholderRole.TABLE => true,

        _ => false,
    };

    /// <summary>
    /// Whether a placeholder of this role carries the content of a slide, rather than describing it.
    /// </summary>
    /// <remarks>
    /// A layout offering nothing but a title and some labels has no room for content. Such layouts are
    /// samples or blank pages, and they are left out of the catalog.
    /// </remarks>
    public static bool CarriesContent(this PlaceholderRole role) => role switch
    {
        PlaceholderRole.SUBTITLE => true,
        PlaceholderRole.TEXT => true,
        PlaceholderRole.PICTURE => true,
        PlaceholderRole.TABLE => true,

        _ => false,
    };

    /// <summary>
    /// How the role is called in the catalog the model reads.
    /// </summary>
    public static string CatalogName(this PlaceholderRole role) => role switch
    {
        PlaceholderRole.TITLE => "title",
        PlaceholderRole.SUBTITLE => "subtitle",
        PlaceholderRole.TEXT => "text",
        PlaceholderRole.LABEL => "label",
        PlaceholderRole.PICTURE => "picture",
        PlaceholderRole.TABLE => "table",

        _ => "unsupported",
    };
}