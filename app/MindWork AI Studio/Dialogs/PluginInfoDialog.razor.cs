using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PluginInfoDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public IPluginMetadata Plugin { get; set; } = null!;

    private string AuthorsText => this.Plugin.Authors.Length > 0 ? string.Join(", ", this.Plugin.Authors) : this.T("No authors specified");

    private string CategoriesText => this.Plugin.Categories.Length > 0 ? string.Join(", ", this.Plugin.Categories.Select(category => category.GetName())) : this.T("No categories specified");

    private string TargetGroupsText => this.Plugin.TargetGroups.Length > 0 ? string.Join(", ", this.Plugin.TargetGroups.Select(targetGroup => targetGroup.Name())) : this.T("No target groups specified");

    private string LastChangedText => this.Plugin.LastChanged?.ToString("d", I18N.I.Culture) ?? "-";

    private void Close() => this.MudDialog.Close();
}
