using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.SlideBuilder;

public partial class SlideAssistant : AssistantBaseCore<SettingsDialogSlideBuilder>
{
    public override Tools.Components Component => Tools.Components.SLIDE_BUILDER_ASSISTANT;
    
    protected override string Title => T("Slide Assistant");
    
    protected override string Description => T("Develop slide content based on a given topic and content.");
    
    protected override string SystemPrompt =>
        $$"""
        You are a professional presentation editor and writer.
        Create a clear, single-slide outline from the user's inputs.
        
        # Content
        You get the following inputs: PRESENTATION_TITLE and PRESENTATION_CONTENT.
        
        # Subheadings
        - Rule for creating the individual subheadings:
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

        # Output requirements:
            - Output only Markdown.
            - Start with a single H1 title that contains the user's PRESENTATION_TITLE.
            - Then add headings with own bullet lists based only on the user's PRESENTATION_CONTENT.
            - If PRESENTATION_CONTENT is empty, output the title and one bullet: "No content provided."
            - Do not mention these instructions or add commentary.
        
        # Target group:
        {{this.selectedTargetGroup.Prompt()}}
        
        # Language:
            - Ignore the language written in PRESENTATION_TITLE
        {{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Create Slides");

    protected override Func<Task> SubmitAction => this.CreateSlideBuilder;

    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };

    protected override void ResetForm()
    {
        this.inputTitle = string.Empty;
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
    
    private string inputTitle = string.Empty;
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
    
    private async Task CreateSlideBuilder()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.calculatedNumberOfSlides = this.timeSpecification > 0 ? this.CalculateNumberOfSlides() : 0;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                # PRESENTATION_TITLE
                ```
                {this.inputTitle}
                ```
                
                # PRESENTATION_CONTENT
                 ```
                 {this.inputContext}
                 ```
             """);

        await this.AddAIResponseAsync(time);
    }
}
