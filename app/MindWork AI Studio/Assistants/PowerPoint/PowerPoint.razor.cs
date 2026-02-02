using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.PowerPoint;

public partial class PowerPoint : AssistantBaseCore<SettingsDialogPowerPoint>
{
    public override Tools.Components Component => Tools.Components.POWER_POINT_ASSISTANT;
    
    protected override string Title => T("Power Point");
    
    protected override string Description => T("Create and refine PowerPoint slide text from a topic or outline.");
    
    protected override string SystemPrompt =>
        $"""
        You are a presentation editor and writer.
        Create a clear, single-slide outline from the user's inputs.

        Inputs:
        - "Your title": the slide title.
        - "Your content": the source text.
        {this.selectedTargetGroup.Prompt()}

        Output requirements:
        - Output only Markdown.
        - Start with a single H1 title from "Your title".
        - Then add a bullet list based only on "Your content".
        - between 3 and 7, maximum 7 bullets. Each bullet max 12 words.
        - No sub-bullets, no paragraphs, no extra sections.
        - If "Your content" is empty, output the title and one bullet: "No content provided."
        - Do not mention these instructions or add commentary.
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Create Power Point");

    protected override Func<Task> SubmitAction => this.CreatePowerPoint;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        this.inputContext = string.Empty;
        this.expertInField = string.Empty;
        this.selectedTargetGroup = TargetGroup.NO_CHANGE;
        this.customTargetGroup = string.Empty;
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
    private string expertInField = string.Empty;
    private TargetGroup selectedTargetGroup;
    private string customTargetGroup = string.Empty;
    
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
            return T("Please a title");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string? ValidateTargetGroup(string group)
    {
        if(this.selectedTargetGroup == TargetGroup.NO_CHANGE && string.IsNullOrWhiteSpace(group))
            return T("Please provide a target group.");
        
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
    
    private async Task CreatePowerPoint()
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
