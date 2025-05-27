using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.Coding;

public partial class CodingContextItem : MSGComponentBase
{
    [Parameter]
    public CodingContext CodingContext { get; set; } = new();

    [Parameter]
    public EventCallback<CodingContext> CodingContextChanged { get; set; }

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
            return string.Format(T("{0}: Please provide your input."), this.CodingContext.Id);
        
        return null;
    }
    
    private string? ValidatingOtherLanguage(string language)
    {
        if(this.CodingContext.Language != CommonCodingLanguages.OTHER)
            return null;
        
        if(string.IsNullOrWhiteSpace(language))
            return T("Please specify the language.");
        
        return null;
    }
}