using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.Synonym;

public partial class AssistantSynonyms : AssistantBaseCore<SettingsDialogSynonyms>
{
    public override Tools.Components Component => Tools.Components.SYNONYMS_ASSISTANT;
    
    protected override string Title => T("Synonyms");
    
    protected override string Description => T("Find synonyms for words or phrases.");
    
    protected override string SystemPrompt => 
        $"""
        You have a PhD in linguistics. Therefore, you are an expert in the {this.SystemPromptLanguage()} language.
        You receive a word or phrase as input. You might also receive some context. You then provide
        a list of synonyms as a Markdown list.
        
        First, derive possible meanings from the word, phrase, and context, when available. Then, provide
        possible synonyms for each meaning.
        
        Example for the word "learn" and the language English (US):
        
        Derive possible meanings (*this list is not part of the output*):
        - Meaning "to learn"
        - Meaning "to retain"
        
        Next, provide possible synonyms for each meaning, which is your output:
        
        # Meaning "to learn"
          - absorb
          - study
          - acquire
          - advance
          - practice
        
        # Meaning "to retain"
          - remember
          - note
          - realize
        
        You do not ask follow-up questions and never repeat the task instructions. When you do not know
        any synonyms for the given word or phrase, you state this. Your output is always in
        the {this.SystemPromptLanguage()} language.
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Find synonyms");

    protected override Func<Task> SubmitAction => this.FindSynonyms;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        this.inputContext = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.Synonyms.PreselectOptions)
        {
            this.selectedLanguage = this.SettingsManager.ConfigurationData.Synonyms.PreselectedLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.Synonyms.PreselectedOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    private string inputText = string.Empty;
    private string inputContext = string.Empty;
    private CommonLanguages selectedLanguage;
    private string customTargetLanguage = string.Empty;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_SYNONYMS_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputContext = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidatingText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide a word or phrase as input.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string SystemPromptLanguage()
    {
        var lang = this.selectedLanguage switch
        {
            CommonLanguages.AS_IS => "source",
            CommonLanguages.OTHER => this.customTargetLanguage,
            
            _ => $"{this.selectedLanguage.Name()}",
        };

        if (string.IsNullOrWhiteSpace(lang))
            return "source";

        return lang;
    }

    private string UserPromptContext()
    {
        if(string.IsNullOrWhiteSpace(this.inputContext))
            return string.Empty;
        
        return $"""
                The given context is:
                
                ```
                {this.inputContext}
                ```
                
                """;
    }
    
    private async Task FindSynonyms()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {this.UserPromptContext()}
                The given word or phrase is:
                
                ```
                {this.inputText}
                ```
             """);

        await this.AddAIResponseAsync(time);
    }
}