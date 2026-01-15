using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.RewriteImprove;

public partial class AssistantRewriteImprove : AssistantBaseCore<SettingsDialogRewrite>
{
    public override Tools.Components Component => Tools.Components.REWRITE_ASSISTANT;
    
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

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

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
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        this.rewrittenText = await this.AddAIResponseAsync(time);
        await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.rewrittenText);
    }
}