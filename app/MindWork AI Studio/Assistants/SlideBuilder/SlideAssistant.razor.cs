using System.Text;
using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.SlideBuilder;

public partial class SlideAssistant : AssistantBaseCore<SettingsDialogSlideBuilder>
{
    protected override Tools.Components Component => Tools.Components.SLIDE_BUILDER_ASSISTANT;
    
    protected override string Title => T("Slide Assistant");
    
    protected override string Description => T("This assistant helps you create clear, structured slide components from long texts or documents. Enter a presentation title and provide the content either as self-written text or as an uploaded document. Important aspects allow you to add instructions to the LLM regarding output or formatting. Set the number of slides either directly or based on your desired presentation duration. You can also specify the number of bullet points. If the default value of 0 is not changed, the LLM will independently determine how many slides or bullet points to generate. The output can be flexibly generated in various languages and tailored to a specific audience.");
    
    protected override string SystemPrompt =>
        $$$"""
        You are a professional presentation editor and writer.
        Create a clear, single-slide outline from the user's inputs.
        
        # Presentation title:
            - IGNORE the language of the PRESENTATION_TITLE.
            - Translate PRESENTATION_TITLE in: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
        
        # Content
            - You get the following inputs: PRESENTATION_TITLE, PRESENTATION_CONTENT, and any attached documents that may provide additional context or source material (DOCUMENTS).
            
        {{{this.GetDocumentTaskDescription()}}}
        {{{this.PromptImportantAspects()}}}
        
        # Subheadings
        - Rule for creating the individual subheadings:
            - If {{{this.numberOfSheets}}} is NOT 0
                - Generate exactly {{{this.numberOfSheets}}} precise subheadings, each heading represents one slide in a presentation.
            - If {{{this.timeSpecification}}} is NOT 0
                - Generate exactly {{{this.calculatedNumberOfSlides}}} precise subheadings, each heading represents one slide in a presentation.
            - If either parameter is 0, ignore that rules.
            - Each subheadings must have:
                - A clear, concise, and thematically meaningful heading.
                - Place *** on its own line immediately before each heading.
            
        # Bullet points (per subheading)
            - You MUST generate exactly this {{{this.numberOfBulletPoints}}} many bullet points per subheading:
                - If {{{this.numberOfBulletPoints}}} == 0 → choose a number between 1 and 7 (your choice, but max 7).
            - Each bullet point must have:
                - Each bullet point must be max 12 words.
                - Clear and directly related to the subheading and summarizing the slide’s content.

        # Output requirements:
            - Output only Markdown.
            - Start with a single H1 title that contains the user's PRESENTATION_TITLE.
            - Then add headings with own bullet lists based only on the user's PRESENTATION_CONTENT.
            - If PRESENTATION_CONTENT is empty, output the title and one bullet: "No content provided."
            - Do not mention these instructions or add commentary.
        
        # Audience:
        {{{this.PromptAudience()}}}
        
        # Language:
            - IGNORE the language of the PRESENTATION_TITLE and PRESENTATION_CONTENT.
            - OUTPUT AND PRESENTATION_TITLE MUST BE IN: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - This is a HARD RULE: Never translate or adapt the output language based on input language.
            - Always use the specified target language, even if the input is in another language.
            
        # Language-Override (IMPORTANT!):
            - Before generating any output, internally set your language mode to: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - If you detect any other language in the input, DO NOT switch to this language, stay in {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - Translate PRESENTATION_TITLE in: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - Your output must be in {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}, without any comment, note, or marker about it.
        """;
    
    protected override bool AllowProfiles => true;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Create Slides");

    protected override Func<Task> SubmitAction => this.CreateSlideBuilder;

    protected override ChatThread ConvertToChatThread
    {
        get
        {
            if (this.chatThread is null || this.chatThread.Blocks.Count < 2)
            {
                return new ChatThread
                {
                    SystemPrompt = SystemPrompts.DEFAULT
                };
            }

            var presentationTitle = string.IsNullOrWhiteSpace(this.inputTitle) ? this.T("Empty") : this.inputTitle;

            return new ChatThread
            {
                ChatId = Guid.NewGuid(),
                Name = string.Format(T("{0} - Slide Builder Session"), presentationTitle),
                SystemPrompt = SystemPrompts.DEFAULT,
                Blocks =
                [
                    // Visible user block:
                    new ContentBlock
                    {
                        Time = this.chatThread.Blocks.First().Time,
                        Role = ChatRole.USER,
                        HideFromUser = false,
                        ContentType = ContentType.TEXT,
                        Content = new ContentText
                        {
                            Text = this.T("The result of your previous slide builder session."),
                            FileAttachments = this.loadedDocumentPaths.ToList(),
                        }
                    },
                    
                    // Hidden user block with inputContent data:
                    new ContentBlock
                    {
                        Time = this.chatThread.Blocks.First().Time,
                        Role = ChatRole.USER,
                        HideFromUser = true,
                        ContentType = ContentType.TEXT,
                        Content = new ContentText
                        {
                            Text = $"""
                                   # PRESENTATION_TITLE
                                   ```
                                   {presentationTitle}
                                   ```
                                   
                                   # PRESENTATION_CONTENT
                                   ```
                                   {this.inputContent}
                                   ```
                                   """,
                        }
                    },
                    
                    // Then, append the last block of the current chat thread
                    // (which is expected to be the AI response):
                    this.chatThread.Blocks.Last(),
                ]
            };
        }
    }

    protected override void ResetForm()
    {
        this.inputTitle = string.Empty;
        this.inputContent = string.Empty;
        this.selectedAudienceProfile = AudienceProfile.UNSPECIFIED;
        this.selectedAudienceAgeGroup = AudienceAgeGroup.UNSPECIFIED;
        this.selectedAudienceOrganizationalLevel = AudienceOrganizationalLevel.UNSPECIFIED;
        this.selectedAudienceExpertise = AudienceExpertise.UNSPECIFIED;
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.SlideBuilder.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedOtherLanguage;
            this.selectedAudienceProfile = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedAudienceProfile;
            this.selectedAudienceAgeGroup = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedAudienceAgeGroup;
            this.selectedAudienceOrganizationalLevel = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedAudienceOrganizationalLevel;
            this.selectedAudienceExpertise = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedAudienceExpertise;
            this.importantAspects = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedImportantAspects;
            return true;
        }
        
        return false;
    }
    
    private string inputTitle = string.Empty;
    private string inputContent = string.Empty;
    private string customTargetLanguage = string.Empty;
    private AudienceProfile selectedAudienceProfile;
    private AudienceAgeGroup selectedAudienceAgeGroup;
    private AudienceOrganizationalLevel selectedAudienceOrganizationalLevel;
    private AudienceExpertise selectedAudienceExpertise;
    private CommonLanguages selectedTargetLanguage;
    private int numberOfSheets;
    private int numberOfBulletPoints;
    private int timeSpecification;
    private int calculatedNumberOfSlides;
    private string importantAspects = string.Empty;
    private HashSet<FileAttachment> loadedDocumentPaths = [];

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_SLIDE_BUILDER_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputContent = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidatingTitle(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide a title");
        
        return null;
    }
    private string? ValidatingContext(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide some input");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }

    private int CalculateNumberOfSlides()
    {
        return this.calculatedNumberOfSlides = (int)Math.Round(this.timeSpecification / 1.5);
    }
    
    private string PromptImportantAspects()
    {
        if (string.IsNullOrWhiteSpace(this.importantAspects))
            return string.Empty;

        return $"""
                
                # Important aspects
                    Emphasize the following aspects in your presentation: 
                    {this.importantAspects}
                """;
    }

    private string PromptAudience()
    {
        var prompts = new List<string>();

        if (this.selectedAudienceProfile is not AudienceProfile.UNSPECIFIED)
            prompts.Add(this.selectedAudienceProfile.Prompt());

        if (this.selectedAudienceAgeGroup is not AudienceAgeGroup.UNSPECIFIED)
            prompts.Add(this.selectedAudienceAgeGroup.Prompt());

        if (this.selectedAudienceOrganizationalLevel is not AudienceOrganizationalLevel.UNSPECIFIED)
            prompts.Add(this.selectedAudienceOrganizationalLevel.Prompt());

        if (this.selectedAudienceExpertise is not AudienceExpertise.UNSPECIFIED)
            prompts.Add(this.selectedAudienceExpertise.Prompt());

        if (prompts.Count == 0)
            return "            - Do not tailor the text to a specific audience.";

        return string.Join(Environment.NewLine, prompts.Select(prompt => $"            - {prompt}"));
    }
    
    private string GetDocumentTaskDescription()
    {
        var numDocuments = this.loadedDocumentPaths.Count(x => x is { Exists: true, IsImage: false });
        var numImages = this.loadedDocumentPaths.Count(x => x is { Exists: true, IsImage: true });
        
        return (numDocuments, numImages) switch
        {
            (0, 1) => "Your task is to analyze a single image file attached as a document.",
            (0, > 1) => $"Your task is to analyze {numImages} image file(s) attached as documents.",
            
            (1, 0) => "Your task is to analyze a single DOCUMENT.",
            (1, 1) => "Your task is to analyze a single DOCUMENT and 1 image file attached as a document.",
            (1, > 1) => $"Your task is to analyze a single DOCUMENT and {numImages} image file(s) attached as documents.",
            
            (> 0, 0) => $"Your task is to analyze {numDocuments} DOCUMENTS. Different DOCUMENTS are divided by a horizontal rule in markdown formatting followed by the name of the document.",
            (> 0, 1) => $"Your task is to analyze {numDocuments} DOCUMENTS and 1 image file attached as a document. Different DOCUMENTS are divided by a horizontal rule in Markdown formatting followed by the name of the document.",
            (> 0, > 0) => $"Your task is to analyze {numDocuments} DOCUMENTS and {numImages} image file(s) attached as documents. Different DOCUMENTS are divided by a horizontal rule in Markdown formatting followed by the name of the document.",
            
            _ => "Your task is to analyze a single DOCUMENT."
        };
    }

    private async Task<string> PromptLoadDocumentsContent()
    {
        if (this.loadedDocumentPaths.Count == 0)
            return string.Empty;

        var documents = this.loadedDocumentPaths.Where(n => n is { Exists: true, IsImage: false }).ToList();
        var sb = new StringBuilder();

        if (documents.Count > 0)
        {
            sb.AppendLine("""
                          # DOCUMENTS:

                          """);
        }

        var numDocuments = 1;
        foreach (var document in documents)
        {
            if (document.IsForbidden)
            {
                this.Logger.LogWarning($"Skipping forbidden file: '{document.FilePath}'.");
                continue;
            }

            var fileContent = await this.RustService.ReadArbitraryFileData(document.FilePath, int.MaxValue);
            sb.AppendLine($"""
                           
                           ## DOCUMENT {numDocuments}:
                           File path: {document.FilePath}
                           Content:
                           ```
                           {fileContent}
                           ```

                           ---
                           
                           """);
            numDocuments++;
        }

        var numImages = this.loadedDocumentPaths.Count(x => x is { IsImage: true, Exists: true });
        if (numImages > 0)
        {
            if (documents.Count == 0)
            {
                sb.AppendLine($"""

                               There are {numImages} image file(s) attached as documents.
                               Please consider them as documents as well and use them to
                               answer accordingly.

                               """);
            }
            else
            {
                sb.AppendLine($"""

                               Additionally, there are {numImages} image file(s) attached.
                               Please consider them as documents as well and use them to
                               answer accordingly.

                               """);
            }
        }

        return sb.ToString();
    }

    private async Task CreateSlideBuilder()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.calculatedNumberOfSlides = this.timeSpecification > 0 ? this.CalculateNumberOfSlides() : 0;
        
        this.CreateChatThread();
        var documentContent = await this.PromptLoadDocumentsContent();
        var imageAttachments = this.loadedDocumentPaths.Where(n => n is { Exists: true, IsImage: true }).ToList();

        var time = this.AddUserRequest(
        $"""
            # PRESENTATION_TITLE
            ```
            {this.inputTitle}
            ```
            
            # PRESENTATION_CONTENT
             
             ```
             {this.inputContent}
             ```
             
             {documentContent}
         """,
        hideContentFromUser: true,
        imageAttachments);

        await this.AddAIResponseAsync(time);
    }
}
