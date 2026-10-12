using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows the details of a plugin on the plugins page: what its authors state about it, and whether an
/// organization manages it.
/// </summary>
/// <remarks>
/// The plugins page lists only the name, the version, and the description of a plugin. Everything else
/// a plugin states, such as its authors or where to get support, would otherwise only be found in its
/// code.
/// </remarks>
public partial class PluginInfoDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The plugin to show. The metadata alone would not do, because only an available plugin knows
    /// whether an organization manages it.
    /// </summary>
    [Parameter]
    public IAvailablePlugin Plugin { get; set; } = null!;

    private string TypeText => this.Plugin.Type.GetName();

    private string AuthorsText => this.Plugin.Authors.Length > 0 ? string.Join(", ", this.Plugin.Authors) : this.T("No authors specified");

    private string CategoriesText => this.Plugin.Categories.Length > 0 ? string.Join(", ", this.Plugin.Categories.Select(category => category.GetName())) : this.T("No categories specified");

    private string TargetGroupsText => this.Plugin.TargetGroups.Length > 0 ? string.Join(", ", this.Plugin.TargetGroups.Select(targetGroup => targetGroup.Name())) : this.T("No target groups specified");

    /// <summary>
    /// The plugin states the time in UTC; the user reads it in their own time zone.
    /// </summary>
    private string LastChangedText => this.Plugin.LastChanged?.ToLocalTime().ToString("g", I18N.I.Culture) ?? this.T("Not specified");

    private void Close() => this.MudDialog.Close();
}
