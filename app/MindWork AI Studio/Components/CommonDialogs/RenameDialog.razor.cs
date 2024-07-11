using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.CommonDialogs;

public partial class RenameDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public string UserInput { get; set; } = string.Empty;

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.UserInput));
}