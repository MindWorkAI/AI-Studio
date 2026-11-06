using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

/// <summary>
/// A group of options in a pane of the settings window, under a small title.
/// </summary>
/// <remarks>
/// The title looks like the headings of the sections in the data source dialogs, and space sets the
/// groups apart, as there. The options keep their own look.
/// </remarks>
public partial class SettingsGroup : ComponentBase
{
    /// <summary>
    /// The title of the group, in the language the user chose.
    /// </summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The options of the group.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}