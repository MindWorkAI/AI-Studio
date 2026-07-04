using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.TextSummarizer;

public partial class AssistantTextSummarizer : AssistantBaseCore<SettingsDialogTextSummarizer>
{
    protected override Tools.Components Component => Tools.Components.TEXT_SUMMARIZER_ASSISTANT;
    
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
    
    protected override string SendToChatVisibleUserPromptText => T("Create a summary of my text");
    
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
    private static readonly AssistantSessionStateKey<bool> SHOW_WEB_CONTENT_READER_STATE_KEY = new(nameof(showWebContentReader));
    private static readonly AssistantSessionStateKey<bool> USE_CONTENT_CLEANER_AGENT_STATE_KEY = new(nameof(useContentCleanerAgent));
    private static readonly AssistantSessionStateKey<string> INPUT_TEXT_STATE_KEY = new(nameof(inputText));
    private static readonly AssistantSessionStateKey<bool> IS_AGENT_RUNNING_STATE_KEY = new(nameof(isAgentRunning));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<Complexity> SELECTED_COMPLEXITY_STATE_KEY = new(nameof(selectedComplexity));
    private static readonly AssistantSessionStateKey<string> EXPERT_IN_FIELD_STATE_KEY = new(nameof(expertInField));
    private static readonly AssistantSessionStateKey<string> IMPORTANT_ASPECTS_STATE_KEY = new(nameof(importantAspects));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(SHOW_WEB_CONTENT_READER_STATE_KEY, this.showWebContentReader);
        state.Set(USE_CONTENT_CLEANER_AGENT_STATE_KEY, this.useContentCleanerAgent);
        state.Set(INPUT_TEXT_STATE_KEY, this.inputText);
        state.Set(IS_AGENT_RUNNING_STATE_KEY, this.isAgentRunning);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(SELECTED_COMPLEXITY_STATE_KEY, this.selectedComplexity);
        state.Set(EXPERT_IN_FIELD_STATE_KEY, this.expertInField);
        state.Set(IMPORTANT_ASPECTS_STATE_KEY, this.importantAspects);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(SHOW_WEB_CONTENT_READER_STATE_KEY, value => this.showWebContentReader = value);
        state.Restore(USE_CONTENT_CLEANER_AGENT_STATE_KEY, value => this.useContentCleanerAgent = value);
        state.Restore(INPUT_TEXT_STATE_KEY, value => this.inputText = value);
        state.Restore(IS_AGENT_RUNNING_STATE_KEY, value => this.isAgentRunning = value);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(SELECTED_COMPLEXITY_STATE_KEY, value => this.selectedComplexity = value);
        state.Restore(EXPERT_IN_FIELD_STATE_KEY, value => this.expertInField = value);
        state.Restore(IMPORTANT_ASPECTS_STATE_KEY, value => this.importantAspects = value);
    }

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
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                Please summarize the following text:
                ```
                {this.inputText}
                ```
             """,
            hideContentFromUser: true);

        await this.AddAIResponseAsync(time);
    }
}
