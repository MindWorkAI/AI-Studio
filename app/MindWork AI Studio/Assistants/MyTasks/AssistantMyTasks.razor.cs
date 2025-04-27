using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;

namespace AIStudio.Assistants.MyTasks;

public partial class AssistantMyTasks : AssistantBaseCore<SettingsDialogMyTasks>
{
    public override Tools.Components Component => Tools.Components.MY_TASKS_ASSISTANT;
    
    protected override string Title => T("My Tasks");
    
    protected override string Description => T("You received a cryptic email that was sent to many recipients and you are now wondering if you need to do something? Copy the email into the input field. You also need to select a personal profile. In this profile, you should describe your role in the organization. The AI will then try to give you hints on what your tasks might be.");
    
    protected override string SystemPrompt => 
        $"""
        You are a friendly and professional business expert. You receive business emails, protocols,
        reports, etc. as input. Additionally, you know the user's role in the organization. The user
        wonders if any tasks arise for them in their role based on the text. You now try to give hints
        and advice on whether and what the user should do. When you believe there are no tasks for the
        user, you tell them this. You consider typical business etiquette in your advice.
        
        You write your advice in the following language: {this.SystemPromptLanguage()}.
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Analyze text");

    protected override Func<Task> SubmitAction => this.AnalyzeText;

    protected override bool ShowProfileSelection => false;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.MyTasks.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.MyTasks.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.MyTasks.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }

    private string inputText = string.Empty;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_MY_TASKS_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidatingText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide some text as input. For example, an email.");
        
        return null;
    }

    private string? ValidateProfile(Profile profile)
    {
        if(profile == default || profile == Profile.NO_PROFILE)
            return T("Please select one of your profiles.");
        
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
        if(this.selectedTargetLanguage is CommonLanguages.AS_IS)
            return "Use the same language as the input";
        
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }
    
    private async Task AnalyzeText()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.inputText);

        await this.AddAIResponseAsync(time);
    }
}