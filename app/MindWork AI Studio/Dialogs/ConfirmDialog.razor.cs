using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// A confirmation dialog that can be used to ask the user for confirmation.
/// </summary>
public partial class ConfirmDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}