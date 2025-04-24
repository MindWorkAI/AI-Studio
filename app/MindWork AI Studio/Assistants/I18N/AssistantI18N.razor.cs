using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.I18N;

public partial class AssistantI18N : AssistantBaseCore<SettingsDialogI18N>
{
    public override Tools.Components Component => Tools.Components.I18N_ASSISTANT;
    
    protected override string Title => "Localization";
    
    protected override string Description =>
        """
        Translate MindWork AI Studio text content into another language.
        """;
    
    protected override string SystemPrompt => 
        """
        TODO
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => "Localize AI Studio";
    
    protected override Func<Task> SubmitAction => this.LocalizeText;
    
    protected override void ResetForm()
    {
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.I18N.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.I18N.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.I18N.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;

    private string? ValidatingTargetLanguage(CommonLanguages language)
    {
        if(language == CommonLanguages.AS_IS)
            return "Please select a target language.";
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return "Please provide a custom language.";
        
        return null;
    }
    
    private async Task LocalizeText()
    {
    }
}