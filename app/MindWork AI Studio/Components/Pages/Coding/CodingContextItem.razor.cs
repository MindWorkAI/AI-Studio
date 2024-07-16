using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Pages.Coding;

public partial class CodingContextItem : ComponentBase
{
    [Parameter]
    public CodingContext CodingContext { get; set; }
    
    [Inject]
    protected SettingsManager SettingsManager { get; set; } = null!;

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnParametersSetAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        
        await base.OnParametersSetAsync();
    }
    
    #endregion
}