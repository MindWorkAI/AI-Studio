using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.Coding;

public partial class CodingContextItem : ComponentBase
{
    [Parameter]
    public CodingContext CodingContext { get; set; } = new();

    [Parameter]
    public EventCallback<CodingContext> CodingContextChanged { get; set; }
    
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
    
    private string? ValidatingCode(string code)
    {
        if(string.IsNullOrWhiteSpace(code))
            return $"{this.CodingContext.Id}: Please provide your input.";
        
        return null;
    }
    
    private string? ValidatingOtherLanguage(string language)
    {
        if(this.CodingContext.Language != CommonCodingLanguages.OTHER)
            return null;
        
        if(string.IsNullOrWhiteSpace(language))
            return "Please specify the language.";
        
        return null;
    }
}