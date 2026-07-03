using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.RewriteImprove;

public partial class AssistantRewriteImprove : AssistantBaseCore<SettingsDialogRewrite>
{
    protected override Tools.Components Component => Tools.Components.REWRITE_ASSISTANT;
    
    protected override string Title => T("Rewrite & Improve Text");
    
    protected override string Description => T("Rewrite and improve your text. Please note, that the capabilities of the different LLM providers will vary.");
    
    protected override string SystemPrompt =>
        $"""
        You are an expert in language and style. You receive a text as input. First, you review the text. If no
        changes are needed, you return the text without modifications. If a change is necessary, you improve the
        text. You can also correct spelling and grammar issues. You never add additional information. You never
        ask the user for additional information. Your response only contains the improved text. You do not explain
        your changes. If no changes are needed, you return the text unchanged.
        The style of the text: {this.selectedWritingStyle.Prompt()}.{this.selectedSentenceStructure.Prompt()}
        You follow the rules according to {this.SystemPromptLanguage()} in all your changes.
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override bool ShowDedicatedProgress => true;
    
    protected override Func<string> Result2Copy => () => this.rewrittenText;

    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new SendToButton
        {
            Self = Tools.Components.REWRITE_ASSISTANT,
            UseResultingContentBlockData = false,
            GetText = () => string.IsNullOrWhiteSpace(this.rewrittenText) ? this.inputText : this.rewrittenText,
        },
    ];
    
    protected override string SubmitText => T("Improve your text");

    protected override Func<Task> SubmitAction => this.RewriteText;

    protected override string SendToChatVisibleUserPromptPrefix => T("Rewrite and improve the following text:");

    protected override string SendToChatVisibleUserPromptContent => this.inputText;

    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        this.rewrittenText = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.selectedWritingStyle = WritingStyles.NOT_SPECIFIED;
            this.selectedSentenceStructure = SentenceStructure.NOT_SPECIFIED;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.RewriteImprove.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.RewriteImprove.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.RewriteImprove.PreselectedOtherLanguage;
            this.selectedWritingStyle = this.SettingsManager.ConfigurationData.RewriteImprove.PreselectedWritingStyle;
            this.selectedSentenceStructure = this.SettingsManager.ConfigurationData.RewriteImprove.PreselectedSentenceStructure;
            return true;
        }
        
        return false;
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_REWRITE_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private string rewrittenText = string.Empty;
    private WritingStyles selectedWritingStyle;
    private SentenceStructure selectedSentenceStructure;
    private static readonly AssistantSessionStateKey<string> INPUT_TEXT_STATE_KEY = new(nameof(inputText));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<string> REWRITTEN_TEXT_STATE_KEY = new(nameof(rewrittenText));
    private static readonly AssistantSessionStateKey<WritingStyles> SELECTED_WRITING_STYLE_STATE_KEY = new(nameof(selectedWritingStyle));
    private static readonly AssistantSessionStateKey<SentenceStructure> SELECTED_SENTENCE_STRUCTURE_STATE_KEY = new(nameof(selectedSentenceStructure));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_TEXT_STATE_KEY, this.inputText);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(REWRITTEN_TEXT_STATE_KEY, this.rewrittenText);
        state.Set(SELECTED_WRITING_STYLE_STATE_KEY, this.selectedWritingStyle);
        state.Set(SELECTED_SENTENCE_STRUCTURE_STATE_KEY, this.selectedSentenceStructure);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_TEXT_STATE_KEY, value => this.inputText = value);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(REWRITTEN_TEXT_STATE_KEY, value => this.rewrittenText = value);
        state.Restore(SELECTED_WRITING_STYLE_STATE_KEY, value => this.selectedWritingStyle = value);
        state.Restore(SELECTED_SENTENCE_STRUCTURE_STATE_KEY, value => this.selectedSentenceStructure = value);
    }
    
    private string? ValidateText(string text)
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
    
    private string SystemPromptLanguage()
    {
        var lang = this.selectedTargetLanguage switch
        {
            CommonLanguages.AS_IS => "the source language",
            CommonLanguages.OTHER => this.customTargetLanguage,
            
            _ => $"{this.selectedTargetLanguage.Name()}",
        };

        if (string.IsNullOrWhiteSpace(lang))
            return "the source language";

        return lang;
    }
    
    private async Task RewriteText()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        this.rewrittenText = await this.AddAIResponseAsync(time);
        if (!this.IsAssistantComponentDisposed)
            await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.rewrittenText);
    }

    protected override async Task OnAssistantSessionRenderedAsync(AssistantSessionSnapshot snapshot)
    {
        if (!snapshot.IsActive && !string.IsNullOrWhiteSpace(this.inputText) && !string.IsNullOrWhiteSpace(this.rewrittenText))
            await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.rewrittenText);
    }
}