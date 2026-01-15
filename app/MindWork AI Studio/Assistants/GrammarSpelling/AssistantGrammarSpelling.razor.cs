using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.GrammarSpelling;

public partial class AssistantGrammarSpelling : AssistantBaseCore<SettingsDialogGrammarSpelling>
{
    public override Tools.Components Component => Tools.Components.GRAMMAR_SPELLING_ASSISTANT;
    
    protected override string Title => T("Grammar & Spelling Checker");
    
    protected override string Description => T("Check the grammar and spelling of a text.");
    
    protected override string SystemPrompt => 
        $"""
        You are an expert in languages and their rules. For example, you know how US and UK English or German in
        Germany and German in Austria differ. You receive text as input. You check the spelling and grammar of
        this text according to the rules of {this.SystemPromptLanguage()}. You never add information. You
        never ask the user for additional information. You do not attempt to improve the wording of the text.
        Your response includes only the corrected text. Do not explain your changes. If no changes are needed,
        you return the text unchanged.
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override bool ShowDedicatedProgress => true;
    
    protected override Func<string> Result2Copy => () => this.correctedText;
    
    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new SendToButton
        {
            Self = Tools.Components.GRAMMAR_SPELLING_ASSISTANT,
            UseResultingContentBlockData = false,
            GetText = () => string.IsNullOrWhiteSpace(this.correctedText) ? this.inputText : this.correctedText
        },
    ];
    
    protected override string SubmitText => T("Proofread");

    protected override Func<Task> SubmitAction => this.ProofreadText;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        this.correctedText = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.GrammarSpelling.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.GrammarSpelling.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.GrammarSpelling.PreselectedOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion

    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private string correctedText = string.Empty;

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

    private async Task ProofreadText()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);
        
        this.correctedText = await this.AddAIResponseAsync(time);
        await this.JsRuntime.GenerateAndShowDiff(this.inputText, this.correctedText);
    }
}