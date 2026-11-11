using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Shows the glossaries outside the settings, e.g. for an assistant which uses them.
/// </summary>
public partial class GlossaryManagementDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private void Close() => this.MudDialog.Close();
}