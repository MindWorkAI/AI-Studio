using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class SecretInputField : MSGComponentBase
{
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    [Parameter]
    public string Secret { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> SecretChanged { get; set; }
    
    [Parameter]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    public string Placeholder { get; set; } = string.Empty;
    
    [Parameter]
    public Func<string, string?> Validation { get; set; } = _ => null;
    
    [Parameter]
    public string Class { get; set; } = "mb-3";

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
    }

    #endregion

    private bool isSecretVisible;
    
    private InputType InputType => this.isSecretVisible ? InputType.Text : InputType.Password;
    
    private string InputTypeIcon => this.isSecretVisible ? Icons.Material.Filled.Visibility : Icons.Material.Filled.VisibilityOff;

    private string ToggleVisibilityTooltip => this.isSecretVisible ? T("Hide content") : T("Show content");

    private Task OnSecretChanged(string arg)
    {
        this.Secret = arg;
        return this.SecretChanged.InvokeAsync(arg);
    }

    private void ToggleVisibility() => this.isSecretVisible = !this.isSecretVisible;
}