using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class SingleInputDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Message { get; set; } = string.Empty;
    
    [Parameter]
    public string UserInput { get; set; } = string.Empty;
    
    [Parameter]
    public string ConfirmText { get; set; } = "OK";

    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Error;
    
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private void Cancel() => this.MudDialog.Cancel();
    
    private void Confirm() => this.MudDialog.Close(DialogResult.Ok(this.UserInput));
}