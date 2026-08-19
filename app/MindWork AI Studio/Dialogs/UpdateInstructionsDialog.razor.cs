using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class UpdateInstructionsDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;

    [Parameter]
    public string? ReleaseUrl { get; set; }

    private void Close() => this.MudDialog.Close();
}