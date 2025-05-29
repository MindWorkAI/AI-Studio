using System.Text;

using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.BiasDay;

public partial class BiasOfTheDayAssistant : AssistantBaseCore<SettingsDialogAssistantBias>
{
    public override Tools.Components Component => Tools.Components.BIAS_DAY_ASSISTANT;
    
    protected override string Title => T("Bias of the Day");
    
    protected override string Description => T("""Learn about a different cognitive bias every day. You can also ask the LLM your questions. The idea behind  "Bias of the Day" is based on work by Buster Benson, John Manoogian III, and Brian Rene Morrissette. Buster Benson grouped the biases, and the original texts come from Wikipedia. Brian Rene Morrissette condensed them into a shorter version. Finally, John Manoogian III created the original poster based on Benson's work and Morrissette's texts. Thorsten Sommer compared all texts for integration into AI Studio with the current Wikipedia versions, updated them, and added source references. The idea of learning about one bias each day based on John's poster comes from Drew Nelson.""");

    protected override string SystemPrompt => $"""
                                              You are a friendly, helpful expert on cognitive bias. You studied psychology and
                                              have a lot of experience. You explain a bias every day. Today's bias belongs to
                                              the category: "{this.biasOfTheDay.Category.ToName()}". We have the following
                                              thoughts on this category:
                                              
                                              {this.biasOfTheDay.Category.GetThoughts()}
                                              
                                              Today's bias is:  
                                              {this.biasOfTheDay.Description}
                                              {this.SystemPromptSources()}
                                              Important: you use the following language: {this.SystemPromptLanguage()}. Please
                                              ask the user a personal question at the end to encourage them to think about
                                              this bias.
                                              """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Show me the bias of the day");

    protected override Func<Task> SubmitAction => this.TellBias;

    protected override bool ShowSendTo => false;
    
    protected override bool ShowCopyResult => false;
    
    protected override bool ShowReset => false;
    
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
        if (this.SettingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.BiasOfTheDay.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.BiasOfTheDay.PreselectedOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    private Bias biasOfTheDay = BiasCatalog.NONE;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    
    private string? ValidateTargetLanguage(CommonLanguages language)
    {
        if(language is CommonLanguages.AS_IS)
            return T("Please select a target language for the bias.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }

    private string SystemPromptSources()
    {
        var sb = new StringBuilder();
        if (this.biasOfTheDay.Links.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Please share the following sources with the user as a Markdown list:");
            foreach (var link in this.biasOfTheDay.Links)
                sb.AppendLine($"- {link}");
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private string SystemPromptLanguage()
    {
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }
    
    private async Task TellBias()
    {
        bool useDrawnBias = false;
        if(this.SettingsManager.ConfigurationData.BiasOfTheDay.RestrictOneBiasPerDay)
        {
            if(this.SettingsManager.ConfigurationData.BiasOfTheDay.DateLastBiasDrawn == DateOnly.FromDateTime(DateTime.Now))
            {
                var biasChat = new LoadChat
                {
                    WorkspaceId = KnownWorkspaces.BIAS_WORKSPACE_ID,
                    ChatId = this.SettingsManager.ConfigurationData.BiasOfTheDay.BiasOfTheDayChatId,
                };

                if (WorkspaceBehaviour.IsChatExisting(biasChat))
                {
                    MessageBus.INSTANCE.DeferMessage(this, Event.LOAD_CHAT, biasChat);
                    this.NavigationManager.NavigateTo(Routes.CHAT);
                    return;
                }
                else
                    useDrawnBias = true;
            }
        }
        
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;

        this.biasOfTheDay = useDrawnBias ?
            BiasCatalog.ALL_BIAS[this.SettingsManager.ConfigurationData.BiasOfTheDay.BiasOfTheDayId] :
            BiasCatalog.GetRandomBias(this.SettingsManager.ConfigurationData.BiasOfTheDay.UsedBias);
        
        var chatId = this.CreateChatThread(KnownWorkspaces.BIAS_WORKSPACE_ID, this.biasOfTheDay.Name);
        this.SettingsManager.ConfigurationData.BiasOfTheDay.BiasOfTheDayId = this.biasOfTheDay.Id;
        this.SettingsManager.ConfigurationData.BiasOfTheDay.BiasOfTheDayChatId = chatId;
        this.SettingsManager.ConfigurationData.BiasOfTheDay.DateLastBiasDrawn = DateOnly.FromDateTime(DateTime.Now);
        await this.SettingsManager.StoreSettings();
        var time = this.AddUserRequest(
             """
             Please tell me about the bias of the day.
             """, true);

        // Start the AI response without waiting for it to finish:
        _ = this.AddAIResponseAsync(time);
        await this.SendToAssistant(Tools.Components.CHAT, default);
    }
}