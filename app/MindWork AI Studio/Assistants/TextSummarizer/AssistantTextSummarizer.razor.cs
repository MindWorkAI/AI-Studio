using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.TextSummarizer;

public partial class AssistantTextSummarizer : AssistantBaseCore<SettingsDialogTextSummarizer>
{
    public override Tools.Components Component => Tools.Components.TEXT_SUMMARIZER_ASSISTANT;
    
    protected override string Title => T("Text Summarizer");
    
    protected override string Description => T("Summarize long text into a shorter version while retaining the main points. You might want to change the language of the summary to make it more readable. It is also possible to change the complexity of the summary to make it easy to understand.");
    
    protected override string SystemPrompt => 
        $"""
         You get a long text as input. The text is marked with ```. The user wants to get a summary of the text.
         {this.selectedTargetLanguage.PromptSummarizing(this.customTargetLanguage)}
         {this.selectedComplexity.Prompt(this.expertInField)}
         {this.PromptImportantAspects()}
         In any case, only use information that is provided in the text for the summary.
         """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Summarize");

    protected override Func<Task> SubmitAction => this.SummarizeText;

    protected override bool SubmitDisabled => this.isAgentRunning;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        if(!this.MightPreselectValues())
        {
            this.showWebContentReader = false;
            this.useContentCleanerAgent = false;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.selectedComplexity = Complexity.NO_CHANGE;
            this.expertInField = string.Empty;
            this.importantAspects = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.TextSummarizer.PreselectOptions)
        {
            this.showWebContentReader = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectWebContentReader;
            this.useContentCleanerAgent = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectContentCleanerAgent;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectedOtherLanguage;
            this.selectedComplexity = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectedComplexity;
            this.expertInField = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectedExpertInField;
            this.importantAspects = this.SettingsManager.ConfigurationData.TextSummarizer.PreselectedImportantAspects;
            return true;
        }
        
        return false;
    }
    
    private bool showWebContentReader;
    private bool useContentCleanerAgent;
    private string inputText = string.Empty;
    private bool isAgentRunning;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private Complexity selectedComplexity;
    private string expertInField = string.Empty;
    private string importantAspects = string.Empty;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidatingText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide a text as input. You might copy the desired text from a document or a website.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string? ValidateExpertInField(string field)
    {
        if(this.selectedComplexity == Complexity.SCIENTIFIC_LANGUAGE_OTHER_EXPERTS && string.IsNullOrWhiteSpace(field))
            return T("Please provide your field of expertise.");
        
        return null;
    }

    private string PromptImportantAspects()
    {
        if (string.IsNullOrWhiteSpace(this.importantAspects))
            return string.Empty;

        return $"""
                Emphasize the following aspects in your summary:
                {this.importantAspects}
                """;
    }

    private async Task SummarizeText()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                Please summarize the following text:
                ```
                {this.inputText}
                ```
             """);

        await this.AddAIResponseAsync(time);
    }
}