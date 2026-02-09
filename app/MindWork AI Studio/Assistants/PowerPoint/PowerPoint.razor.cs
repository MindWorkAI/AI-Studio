using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.PowerPoint;

public partial class PowerPoint : AssistantBaseCore<SettingsDialogPowerPoint>
{
    public override Tools.Components Component => Tools.Components.POWER_POINT_ASSISTANT;
    
    protected override string Title => T("Power Point");
    
    protected override string Description => T("Create and refine PowerPoint slide text from a topic or outline.");
    
    protected override string SystemPrompt =>
        $$"""
        You are a professional presentation editor and writer.
        Create a clear, single-slide outline from the user's inputs.
        {{this.selectedTargetLanguage.PromptTranslation(this.customTargetLanguage)}}

        Inputs:
        - "Your title": the main title. 
        {{this.inputText}}
        - "Your content": the source text.
        {{this.selectedTargetGroup.Prompt()}}
        
        Rule for creating the individual subheadings:
            - If {{this.numberOfSheets}} is NOT 0
                - Generate exactly {{this.numberOfSheets}} precise subheadings, each heading represents one slide in a presentation.
            - If {{this.timeSpecification}} is NOT 0
                - Generate exactly {{this.calculatedNumberOfSlides}} precise subheadings, each heading represents one slide in a presentation.
            - If either parameter is 0, ignore that rules.
        
        - Each subheadings must have:
            - A clear, concise, and thematically meaningful heading.
            - 1 to 7 bullet points (maximum 7) summarizing the slide’s content — use as many as needed, but never more than 7.
            - Each bullet point must be max 12 words.
            - Place *** on its own line immediately before each heading.

        Output requirements:
        - Output only Markdown.
        - Start with a single H1 title from "Your title".
        - Then add headings with own bullet lists based only on "Your content".
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
    private CommonLanguages selectedTargetLanguage;
    private double numberOfSheets;
    private double timeSpecification;
    private int calculatedNumberOfSlides = 0;

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

    private int CalculateNumberOfSlides()
    {
        return this.calculatedNumberOfSlides = (int)Math.Round(this.timeSpecification / 1.5);
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
        
        this.calculatedNumberOfSlides = this.timeSpecification > 0 ? this.CalculateNumberOfSlides() : 0;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {this.UserPromptContext()}
                
                ```
                {this.inputText}
                ```
             """);

        await this.AddAIResponseAsync(time);
    }
}
