using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace AIStudio.Components.CommonDialogs;

/// <summary>
/// A confirmation dialog that can be used to ask the user for confirmation.
/// </summary>
public partial class ConfirmDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));
}