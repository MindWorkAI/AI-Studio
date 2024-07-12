using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.CommonDialogs;

public partial class SingleInputDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public string UserInput { get; set; } = string.Empty;
    
    [Parameter]
    public string ConfirmText { get; set; } = "OK";

    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Error;

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.UserInput));
}