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

    [Parameter]
    public bool AllowEmptyInput { get; set; }
    
    [Parameter]
    public string InputHeaderText { get; set; } = string.Empty;

    [Parameter]
    public string EmptyInputErrorMessage { get; set; } = string.Empty;

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();

    private MudForm form = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private string GetInputHeaderText => string.IsNullOrWhiteSpace(this.InputHeaderText) ? T("Your Input") : this.InputHeaderText;
    
    private string? ValidateUserInput(string? value)
    {
        if (!this.AllowEmptyInput && string.IsNullOrWhiteSpace(value))
            return string.IsNullOrWhiteSpace(this.EmptyInputErrorMessage) ? T("Please enter a value.") : this.EmptyInputErrorMessage;
        
        return null;
    }
    
    private void Cancel() => this.MudDialog.Cancel();
    
    private async Task Confirm()
    {
        await this.form.Validate();
        if(!this.form.IsValid)
            return;
        
        this.MudDialog.Close(DialogResult.Ok(this.UserInput));
    }
}