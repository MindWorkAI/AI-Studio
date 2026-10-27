using System.Globalization;

namespace AIStudio.Tools.PowerPoint;

internal static class PlaceholderRoleExtensions
{
    private static readonly PlaceholderRole[] SLOT_ROLES = [PlaceholderRole.TITLE, PlaceholderRole.SUBTITLE, PlaceholderRole.TEXT, PlaceholderRole.LABEL, PlaceholderRole.PICTURE, PlaceholderRole.TABLE];

    /// <summary>
    /// Reads the name of a slot as the catalog gives it to the model, e.g., "text 2" or "title".
    /// </summary>
    /// <remarks>
    /// The catalog leaves out the number where a layout has a single slot of a role, as it does for
    /// the title; the first slot is meant then. Case and spaces do not matter.
    /// </remarks>
    /// <param name="name">The name.</param>
    /// <param name="role">The role of the slot.</param>
    /// <param name="ordinal">The number of the slot among the slots of its role, starting at 1.</param>
    /// <returns>Whether the name is the name of a slot.</returns>
    public static bool TryParseSlotName(string name, out PlaceholderRole role, out int ordinal)
    {
        role = PlaceholderRole.UNSUPPORTED;
        ordinal = 0;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > 2)
            return false;

        foreach (var candidate in SLOT_ROLES)
        {
            if (!string.Equals(words[0], candidate.CatalogName(), StringComparison.OrdinalIgnoreCase))
                continue;

            var number = 1;
            if (words.Length == 2 && (!int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out number) || number < 1))
                return false;

            role = candidate;
            ordinal = number;
            return true;
        }

        return false;
    }

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