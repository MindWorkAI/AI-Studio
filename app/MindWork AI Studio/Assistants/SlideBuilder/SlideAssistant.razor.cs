using System.Text;
using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.SlideBuilder;

public partial class SlideAssistant : AssistantBaseCore<SettingsDialogSlideBuilder>
{
    protected override Tools.Components Component => Tools.Components.SLIDE_BUILDER_ASSISTANT;
    
    protected override string Title => T("Slide Planner Assistant");
    
    protected override string Description => T("This assistant helps you create clear, structured slides from long texts or documents. Enter a presentation title and provide the content either as text or with one or more documents. Important aspects allow you to add instructions to the LLM regarding output or formatting. Set the number of slides either directly or based on your desired presentation duration. You can also specify the number of bullet points. If the default value of 0 is not changed, the LLM will independently determine how many slides or bullet points to generate. The output can be flexibly generated in various languages and tailored to a specific audience.");
    
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
            - Then add headings with own bullet lists based on the provided source material: PRESENTATION_CONTENT, DOCUMENTS, and attached images.
            - If both PRESENTATION_CONTENT and attached source material are provided, use all of them, while prioritizing direct user instructions from PRESENTATION_CONTENT when resolving ambiguity.
            - If PRESENTATION_CONTENT is empty but attached source material is available, create the slides from the attached source material.
            - If neither PRESENTATION_CONTENT nor any attached source material is available, output the title and one bullet: "No content provided."
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
            if (this.ChatThread is null || this.ChatThread.Blocks.Count < 2)
            {
                return new ChatThread
                {
                    SystemPrompt = SystemPrompts.DEFAULT
                };
            }
            
            return new ChatThread
            {
                ChatId = Guid.NewGuid(),
                Name = string.Format(T("{0} - Slide Builder Session"), this.inputTitle),
                SystemPrompt = SystemPrompts.DEFAULT,
                Blocks =
                [
                    // Visible user block:
                    new ContentBlock
                    {
                        Time = this.ChatThread.Blocks.First().Time,
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
                        Time = this.ChatThread.Blocks.First().Time,
                        Role = ChatRole.USER,
                        HideFromUser = true,
                        ContentType = ContentType.TEXT,
                        Content = new ContentText
                        {
                            Text = string.IsNullOrWhiteSpace(this.inputContent)
                                ? $"""
                                   # PRESENTATION_TITLE
                                   ```
                                   {this.inputTitle}
                                   ```
                                   """
                                
                                : $"""
                                   # PRESENTATION_TITLE
                                   ```
                                   {this.inputTitle}
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
                    this.ChatThread.Blocks.Last(),
                ]
            };
        }
    }

    protected override void ResetForm()
    {
        this.inputTitle = string.Empty;
        this.inputContent = string.Empty;
        this.loadedDocumentPaths.Clear();
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
    private static readonly AssistantSessionStateKey<string> INPUT_TITLE_STATE_KEY = new(nameof(inputTitle));
    private static readonly AssistantSessionStateKey<string> INPUT_CONTENT_STATE_KEY = new(nameof(inputContent));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<AudienceProfile> SELECTED_AUDIENCE_PROFILE_STATE_KEY = new(nameof(selectedAudienceProfile));
    private static readonly AssistantSessionStateKey<AudienceAgeGroup> SELECTED_AUDIENCE_AGE_GROUP_STATE_KEY = new(nameof(selectedAudienceAgeGroup));
    private static readonly AssistantSessionStateKey<AudienceOrganizationalLevel> SELECTED_AUDIENCE_ORGANIZATIONAL_LEVEL_STATE_KEY = new(nameof(selectedAudienceOrganizationalLevel));
    private static readonly AssistantSessionStateKey<AudienceExpertise> SELECTED_AUDIENCE_EXPERTISE_STATE_KEY = new(nameof(selectedAudienceExpertise));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<int> NUMBER_OF_SHEETS_STATE_KEY = new(nameof(numberOfSheets));
    private static readonly AssistantSessionStateKey<int> NUMBER_OF_BULLET_POINTS_STATE_KEY = new(nameof(numberOfBulletPoints));
    private static readonly AssistantSessionStateKey<int> TIME_SPECIFICATION_STATE_KEY = new(nameof(timeSpecification));
    private static readonly AssistantSessionStateKey<int> CALCULATED_NUMBER_OF_SLIDES_STATE_KEY = new(nameof(calculatedNumberOfSlides));
    private static readonly AssistantSessionStateKey<string> IMPORTANT_ASPECTS_STATE_KEY = new(nameof(importantAspects));
    private static readonly AssistantSessionStateKey<HashSet<FileAttachment>> LOADED_DOCUMENT_PATHS_STATE_KEY = new(nameof(loadedDocumentPaths));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_TITLE_STATE_KEY, this.inputTitle);
        state.Set(INPUT_CONTENT_STATE_KEY, this.inputContent);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(SELECTED_AUDIENCE_PROFILE_STATE_KEY, this.selectedAudienceProfile);
        state.Set(SELECTED_AUDIENCE_AGE_GROUP_STATE_KEY, this.selectedAudienceAgeGroup);
        state.Set(SELECTED_AUDIENCE_ORGANIZATIONAL_LEVEL_STATE_KEY, this.selectedAudienceOrganizationalLevel);
        state.Set(SELECTED_AUDIENCE_EXPERTISE_STATE_KEY, this.selectedAudienceExpertise);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(NUMBER_OF_SHEETS_STATE_KEY, this.numberOfSheets);
        state.Set(NUMBER_OF_BULLET_POINTS_STATE_KEY, this.numberOfBulletPoints);
        state.Set(TIME_SPECIFICATION_STATE_KEY, this.timeSpecification);
        state.Set(CALCULATED_NUMBER_OF_SLIDES_STATE_KEY, this.calculatedNumberOfSlides);
        state.Set(IMPORTANT_ASPECTS_STATE_KEY, this.importantAspects);
        state.SetHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_TITLE_STATE_KEY, value => this.inputTitle = value);
        state.Restore(INPUT_CONTENT_STATE_KEY, value => this.inputContent = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(SELECTED_AUDIENCE_PROFILE_STATE_KEY, value => this.selectedAudienceProfile = value);
        state.Restore(SELECTED_AUDIENCE_AGE_GROUP_STATE_KEY, value => this.selectedAudienceAgeGroup = value);
        state.Restore(SELECTED_AUDIENCE_ORGANIZATIONAL_LEVEL_STATE_KEY, value => this.selectedAudienceOrganizationalLevel = value);
        state.Restore(SELECTED_AUDIENCE_EXPERTISE_STATE_KEY, value => this.selectedAudienceExpertise = value);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(NUMBER_OF_SHEETS_STATE_KEY, value => this.numberOfSheets = value);
        state.Restore(NUMBER_OF_BULLET_POINTS_STATE_KEY, value => this.numberOfBulletPoints = value);
        state.Restore(TIME_SPECIFICATION_STATE_KEY, value => this.timeSpecification = value);
        state.Restore(CALCULATED_NUMBER_OF_SLIDES_STATE_KEY, value => this.calculatedNumberOfSlides = value);
        state.Restore(IMPORTANT_ASPECTS_STATE_KEY, value => this.importantAspects = value);
        state.RestoreHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
    }

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
        if(string.IsNullOrWhiteSpace(text) && !this.HasValidInputDocuments())
            return T("Please provide a text or at least one valid document or image.");
        
        return null;
    }

    private bool HasValidInputDocuments() => this.loadedDocumentPaths.Any(n => n is { Exists: true });

    private async Task OnDocumentsChanged(HashSet<FileAttachment> _)
    {
        if(this.Form is not null)
            await this.Form.Validate();
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
        await this.Form!.Validate();
        if (!this.InputIsValid)
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
