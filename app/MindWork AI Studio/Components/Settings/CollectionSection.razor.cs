using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// A collection, such as the chat templates, shown on the page of another setting: its title, what it
/// is, a few of its entries, a way to add one, and "Manage" for its own page.
/// </summary>
/// <remarks>
/// <para>
/// The section only shows what it gets. It never picks, sorts, cuts, or groups entries: each collection
/// does that for itself and hands over a <see cref="CollectionPreview"/>, so the section serves
/// collections which sort in different ways, such as providers grouped by company.
/// </para>
/// <para>
/// Entries the user cannot change, such as those of an organization, stay out of the preview. The page
/// of the collection still lists them.
/// </para>
/// <para>
/// The whole section is a <see cref="SettingsAnchor"/>: coming back from the page of the collection, the
/// settings window shows the section again, whichever entry the user opened or deleted there.
/// </para>
/// </remarks>
public partial class CollectionSection : MSGComponentBase
{
    /// <summary>
    /// How many entries a preview shows at most, the same for every collection.
    /// </summary>
    /// <remarks>
    /// Two keep the preview on one line in most cases and on two at most, also with names of the 40
    /// characters the dialogs allow, beside "+N more" and the chip for a new entry.
    /// </remarks>
    public const int MAX_PREVIEW_ITEMS = 2;

    /// <summary>
    /// The path of the page of the collection, whose title the section takes and where "Manage" leads.
    /// </summary>
    [Parameter]
    public SettingsPath Path { get; set; }

    /// <summary>
    /// What the collection is, in one line.
    /// </summary>
    [Parameter]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The entries to show, see <see cref="CollectionPreview"/>.
    /// </summary>
    [Parameter]
    public CollectionPreview Preview { get; set; } = new([], 0);

    /// <summary>
    /// Whether the user may add entries, which the chip for a new one needs.
    /// </summary>
    [Parameter]
    public bool CanCreate { get; set; }

    /// <summary>
    /// What the chip for a new entry says, such as "New template".
    /// </summary>
    [Parameter]
    public string CreateText { get; set; } = string.Empty;

    /// <summary>
    /// What the chip for a new entry does.
    /// </summary>
    [Parameter]
    public EventCallback OnCreate { get; set; }

    private string Title => SettingsPanes.Find(this.Path)?.Title() ?? string.Empty;
}