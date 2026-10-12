using System.Text;
using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

namespace AIStudio.Assistants.SlideBuilder;

public partial class SlideAssistant : AssistantBaseCore<SettingsDialogSlideBuilder>
{
    protected override Tools.Components Component => Tools.Components.SLIDE_BUILDER_ASSISTANT;
    
    protected override string Title => T("PowerPoint Creator Assistant");
    
    protected override string Description => T("This assistant creates a native PowerPoint presentation from your content and a PowerPoint template. The generated slides retain the template's masters, theme, and layouts. You can review the slide plan before exporting the .pptx file.");
    
    protected override string SystemPrompt =>
        $$$"""
        You are a professional presentation editor and writer.
        Create a concise slide-deck plan from the user's inputs. The application will render this plan into a native PowerPoint presentation.
        
        # Presentation title:
            - IGNORE the language of the PRESENTATION_TITLE.
            - Translate PRESENTATION_TITLE in: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
        
        # Content
            - You get the following inputs: PRESENTATION_TITLE, PRESENTATION_CONTENT, and any attached documents that may provide additional context or source material (DOCUMENTS).
            
        {{{this.GetDocumentTaskDescription()}}}
        {{{this.PromptImportantAspects()}}}
        
        # Slides
            - The title field represents the title slide.
            - Each item in the slides array represents one content slide after the title slide.
            - The requested total number of slides is {{{this.RequestedTotalSlides}}}, including the title slide.
            - If the requested total is greater than 0, generate exactly that many total slides.
            - If the requested total is 0, choose between 2 and 20 total slides, including the title slide.
            - Every content slide needs a clear, concise, and thematically meaningful title.
            
        # Bullet points (per subheading)
            - You MUST generate exactly this {{{this.numberOfBulletPoints}}} many bullet points per content slide:
                - If {{{this.numberOfBulletPoints}}} == 0 → choose a number between 1 and 7 (your choice, but max 7).
            - Each bullet point must have:
                - Each bullet point must be max 12 words.
                - Clear and directly related to the subheading and summarizing the slide’s content.

        # Output requirements:
            - Set contractVersion to {{{SlideDeckPlanProcessor.CONTRACT_VERSION}}}.
            - Use the title and slides fields based on the provided source material: PRESENTATION_CONTENT, DOCUMENTS, and attached images.
            - If both PRESENTATION_CONTENT and attached source material are provided, use all of them, while prioritizing direct user instructions from PRESENTATION_CONTENT when resolving ambiguity.
            - If PRESENTATION_CONTENT is empty but attached source material is available, create the slides from the attached source material.
            - Do not mention these instructions or add commentary.

        {{{SlideDeckPlanProcessor.ContractGrammar}}}

        # JSON transport rules:
            - Answer with the bare JSON object and nothing else: no explanation, Markdown, or code fence.
            - Use double-quoted property names and string values.
            - Do not use comments, trailing commas, ellipses, or unescaped multiline strings.
            - Before sending, verify that the root object is closed and every property conforms to the grammar.
        
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

    protected override bool ShowResult => this.ResultingContentBlock is not { HideFromUser: true };
    
    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new ButtonData
        {
            Text = T("Export PowerPoint"),
            Icon = Icons.Material.Filled.Slideshow,
            Color = Color.Primary,
            Tooltip = T("Create a .pptx file from the reviewed slide plan and the selected template."),
            AsyncAction = this.ExportPowerPoint,
            DisabledActionParam = () => this.slideDeckPlan is null || string.IsNullOrWhiteSpace(this.powerPointTemplatePath) || this.IsProcessing,
        },
    ];
    
    protected override string SubmitText => T("Create presentation plan");

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
        this.powerPointTemplatePath = string.Empty;
        this.slideDeckPlan = null;
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
    private string powerPointTemplatePath = string.Empty;
    private SlideDeckPlan? slideDeckPlan;
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
    private static readonly AssistantSessionStateKey<string> POWER_POINT_TEMPLATE_PATH_STATE_KEY = new(nameof(powerPointTemplatePath));
    private static readonly AssistantSessionStateKey<SlideDeckPlan?> SLIDE_DECK_PLAN_STATE_KEY = new(nameof(slideDeckPlan));
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
        state.Set(POWER_POINT_TEMPLATE_PATH_STATE_KEY, this.powerPointTemplatePath);
        state.Set(SLIDE_DECK_PLAN_STATE_KEY, this.slideDeckPlan);
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
        state.Restore(POWER_POINT_TEMPLATE_PATH_STATE_KEY, value => this.powerPointTemplatePath = value);
        state.Restore(SLIDE_DECK_PLAN_STATE_KEY, value => this.slideDeckPlan = value);
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
        var deferredContent = MessageBus.INSTANCE.TakeDeferredMessages<string>(Event.SEND_TO_SLIDE_BUILDER_ASSISTANT).LastOrDefault();
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

    private string? ValidatingPowerPointTemplate(string path)
    {
        var result = PowerPointTemplateAnalyzer.Analyze(path);
        return result.Analysis is null ? this.LocalizePowerPointIssue(result.Issue) : null;
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
        return this.calculatedNumberOfSlides = Math.Clamp(
            (int)Math.Round(this.timeSpecification / 1.5),
            2,
            SlideDeckPlanProcessor.MAX_TOTAL_SLIDES);
    }

    private int RequestedTotalSlides => this.numberOfSheets > 0
        ? this.numberOfSheets
        : this.calculatedNumberOfSlides;
    
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

        //
        // One report for the whole batch: reading twenty documents must produce one dialog
        // listing all of them, not twenty dialogs in a row.
        //
        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();
        await using var promptInjectionScope = guardService.BeginAction();

        var numDocuments = 1;
        foreach (var document in documents)
        {
            if (document.IsForbidden)
            {
                this.Logger.LogWarning($"Skipping forbidden file: '{document.FilePath}'.");
                continue;
            }

            var extraction = await this.RustService.ReadArbitraryFileData(document.FilePath, int.MaxValue);
            if (!extraction.HasUsableContent)
            {
                this.Logger.LogError("Reading the document '{FilePath}' failed and it will not be used: code={ErrorCode}, message='{ErrorMessage}'.", document.FilePath, extraction.ErrorCode, extraction.ErrorMessage);
                await this.MessageBus.SendError(new(Icons.Material.Filled.Description, extraction.ToUserMessage(document.FileName)));
                continue;
            }

            if (extraction.Outcome is FileExtractionOutcome.PARTIAL)
            {
                this.Logger.LogWarning("Parts of the document '{FilePath}' could not be read: pages={FailedPages}.", document.FilePath, string.Join(", ", extraction.FailedPages));
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.Description, extraction.ToPartialUserMessage(document.FileName)));
            }

            // The file was read correctly, but its extension lies about what it contains:
            if (extraction.HasExtensionMismatch)
            {
                this.Logger.LogWarning("The document '{FilePath}' is actually a '{DetectedFormat}'.", document.FilePath, extraction.DetectedFormat);
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.RuleFolder, extraction.ToExtensionMismatchUserMessage(document.FileName)));
            }

            var fileContent = extraction.Content;
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

        var templateResult = PowerPointTemplateAnalyzer.Analyze(this.powerPointTemplatePath);
        if (templateResult.Analysis is null)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, this.LocalizePowerPointIssue(templateResult.Issue)));
            return;
        }
        
        this.calculatedNumberOfSlides = this.timeSpecification > 0 ? this.CalculateNumberOfSlides() : 0;
        this.slideDeckPlan = null;
        
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

        var answer = await this.AddAIResponseAsync(time, hideContentFromUser: true);
        var processing = SlideDeckPlanProcessor.Process(answer, this.RequestedTotalSlides, this.numberOfBulletPoints);
        if (processing.Plan is null)
        {
            var repairTime = this.AddUserRequest(
                SlideDeckPlanProcessor.BuildRepairPrompt(processing),
                hideContentFromUser: true);
            answer = await this.AddAIResponseAsync(repairTime, hideContentFromUser: true);
            processing = SlideDeckPlanProcessor.Process(answer, this.RequestedTotalSlides, this.numberOfBulletPoints);
        }

        if (processing.Plan is null)
        {
            this.ResultingContentBlock = null;
            await this.MessageBus.SendError(new(
                Icons.Material.Filled.DataObject,
                T("The model did not return a valid presentation plan after one repair attempt. Please try again.")));
            return;
        }

        this.slideDeckPlan = processing.Plan;
        var preview = new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            HideFromUser = false,
            Content = new ContentText
            {
                Text = SlideDeckPlanProcessor.ToMarkdown(this.slideDeckPlan),
            },
        };
        this.ChatThread!.Blocks.Add(preview);
        this.ResultingContentBlock = preview;
    }

    private async Task ExportPowerPoint()
    {
        if (this.slideDeckPlan is null)
            return;

        var templateResult = PowerPointTemplateAnalyzer.Analyze(this.powerPointTemplatePath);
        if (templateResult.Analysis is null)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, this.LocalizePowerPointIssue(templateResult.Issue)));
            return;
        }

        var response = await this.RustService.SaveFile(
            T("Save PowerPoint presentation"),
            [FileTypes.POWER_POINT_OPEN_XML],
            $"{this.SafePresentationFileName()}.pptx");
        if (response.UserCancelled)
            return;

        var renderResult = PowerPointPresentationRenderer.Render(
            this.powerPointTemplatePath,
            response.SaveFilePath,
            this.slideDeckPlan,
            templateResult.Analysis);
        if (!renderResult.Success)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, this.LocalizePowerPointIssue(renderResult.Issue)));
            return;
        }

        await this.MessageBus.SendSuccess(new(
            Icons.Material.Filled.Slideshow,
            string.Format(T("The PowerPoint presentation was saved to '{0}'."), renderResult.OutputPath)));
    }

    private string SafePresentationFileName()
    {
        var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
        var safeName = new string(this.inputTitle.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? T("Presentation") : safeName;
    }

    private string LocalizePowerPointIssue(string issue) => issue switch
    {
        "Please select a PowerPoint template." => T("Please select a PowerPoint template."),
        "The selected PowerPoint template is no longer available." => T("The selected PowerPoint template is no longer available."),
        "The template must be a PowerPoint .pptx file." => T("The template must be a PowerPoint .pptx file."),
        "The selected file does not contain a PowerPoint presentation." => T("The selected file does not contain a PowerPoint presentation."),
        "The PowerPoint template does not contain a title layout with a title placeholder." => T("The PowerPoint template does not contain a title layout with a title placeholder."),
        "The PowerPoint template does not contain a layout with title and content placeholders." => T("The PowerPoint template does not contain a layout with title and content placeholders."),
        "The selected file is not a readable PowerPoint presentation." => T("The selected file is not a readable PowerPoint presentation."),
        "The selected PowerPoint template could not be read." => T("The selected PowerPoint template could not be read."),
        "The selected PowerPoint template could not be accessed." => T("The selected PowerPoint template could not be accessed."),
        "The generated presentation must not overwrite its template." => T("The generated presentation must not overwrite its template."),
        "The selected output folder is not available." => T("The selected output folder is not available."),
        "The generated PowerPoint presentation did not pass Open XML validation." => T("The generated PowerPoint presentation did not pass Open XML validation."),
        "The PowerPoint presentation could not be generated from this template." => T("The PowerPoint presentation could not be generated from this template."),
        "The PowerPoint presentation could not be written to the selected location." => T("The PowerPoint presentation could not be written to the selected location."),
        "The PowerPoint presentation could not be written because access was denied." => T("The PowerPoint presentation could not be written because access was denied."),
        _ => T("The PowerPoint presentation could not be created."),
    };
}
