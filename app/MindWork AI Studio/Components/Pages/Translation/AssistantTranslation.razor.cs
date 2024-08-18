using AIStudio.Chat;
using AIStudio.Tools;

namespace AIStudio.Components.Pages.Translation;

public partial class AssistantTranslation : AssistantBaseCore
{
    protected override string Title => "Translation";
    
    protected override string Description =>
        """
        Translate text from one language to another.
        """;
    
    protected override string SystemPrompt => 
        """
        You get text in a source language as input. The user wants to get the text translated into a target language.
        Provide the translation in the requested language. Do not add any information. Correct any spelling or grammar mistakes.
        Do not ask for additional information. Do not mirror the user's language. Do not mirror the task. When the target
        language requires, e.g., shorter sentences, you should split the text into shorter sentences.
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new SendToButton
        {
            Self = SendTo.TRANSLATION_ASSISTANT,
        },
    ];
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetFrom()
    {
        this.inputText = string.Empty;
        this.inputTextLastTranslation = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.liveTranslation = false;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.Translation.PreselectOptions)
        {
            this.liveTranslation = this.SettingsManager.ConfigurationData.Translation.PreselectLiveTranslation;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.Translation.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.Translation.PreselectOtherLanguage;
            this.providerSettings = this.SettingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.SettingsManager.ConfigurationData.Translation.PreselectedProvider);
            return true;
        }
        
        return false;
    }
    
    private bool liveTranslation;
    private bool isAgentRunning;
    private string inputText = string.Empty;
    private string inputTextLastTranslation = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MightPreselectValues();
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_TRANSLATION_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidatingText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return "Please provide a text as input. You might copy the desired text from a document or a website.";
        
        return null;
    }
    
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

    private async Task TranslateText(bool force)
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        if(!force && this.inputText == this.inputTextLastTranslation)
            return;
        
        this.inputTextLastTranslation = this.inputText;
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {this.selectedTargetLanguage.PromptTranslation(this.customTargetLanguage)}
                
                The given text is:
                
                ---
                {this.inputText}
             """);

        await this.AddAIResponseAsync(time);
    }
}