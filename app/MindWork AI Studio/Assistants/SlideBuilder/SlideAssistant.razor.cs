using System.Text;
using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PowerPoint;
using AIStudio.Tools.Security;

namespace AIStudio.Assistants.SlideBuilder;

public partial class SlideAssistant : AssistantBaseCore<SettingsDialogSlideBuilder>
{
    protected override Tools.Components Component => Tools.Components.SLIDE_BUILDER_ASSISTANT;
    
    protected override string Title => T("Slide Planner Assistant");
    
    protected override string Description => T("This assistant helps you create clear, structured slides from long texts or documents. Enter a presentation title and provide the content either as text or with one or more documents. Important aspects allow you to add instructions to the LLM regarding output or formatting. Set the number of slides either directly or based on your desired presentation duration. You can also limit the number of bullet points per slide. If the default value of 0 is not changed, the LLM will independently determine how many slides or bullet points to generate. The output can be flexibly generated in various languages and tailored to a specific audience. When you choose a PowerPoint template, the slides use its layouts, and you can create a PowerPoint file in its design from the result.");
    
    protected override string SystemPrompt =>
        $$$"""
        You are a professional presentation editor and writer.
        Plan a presentation from the user's inputs and describe it slide by slide as JSON, using the slide layouts LAYOUTS lists.

        # Presentation title:
            - IGNORE the language of the PRESENTATION_TITLE.
            - Translate PRESENTATION_TITLE in: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}

        # Content
            - You get the following inputs: PRESENTATION_TITLE, PRESENTATION_CONTENT, the layouts the slides use (LAYOUTS), and any attached documents (DOCUMENTS) or pictures (PICTURES) that may provide additional context or source material.
            - Base the slides on the provided source material: PRESENTATION_CONTENT, DOCUMENTS, and attached images.
            - If both PRESENTATION_CONTENT and attached source material are provided, use all of them, while prioritizing direct user instructions from PRESENTATION_CONTENT when resolving ambiguity.
            - If PRESENTATION_CONTENT is empty but attached source material is available, create the slides from the attached source material.
            - If neither PRESENTATION_CONTENT nor any attached source material is available, create a single slide with the title and the text "No content provided."

        {{{this.GetDocumentTaskDescription()}}}
        {{{this.PromptImportantAspects()}}}

        # Layouts
        Every slide uses one of the layouts LAYOUTS lists. A layout offers slots, listed in reading order with their place on the slide and how much text fits.
            - Choose for each slide the layout whose slots suit its content, and vary the layouts where the content allows.
            - Fill only slots the layout offers, and stay within the stated lines and characters. Spread content over more slides rather than overfilling one.
            - A label is a short text, e.g., a heading above a text or the source below a picture.
            - Set "starts_chapter" to true on each slide which opens a new part of the presentation. Where a layout is meant for chapters or sections, use it for these slides.
        {{{this.PromptChosenLayouts()}}}
        {{{this.PromptTemplateSlides()}}}

        # Pictures
        {{{this.PromptPictures()}}}

        # Slides
        {{{this.PromptSlideCount()}}}
            - Every slide has a clear, concise, and thematically meaningful title.

        # Bullet points (per text slot)
        {{{this.PromptBulletPoints()}}}

        # Output format:
        Answer with one JSON object and nothing else: no explanation, no Markdown, and no code fence.
        {
          "title": "<the presentation title>",
          "slides": [
            {
              "layout": "<the ID of a layout, e.g., L3>",
              "starts_chapter": false,
              "title": "<the slide title>",
              "subtitle": "<only for layouts with a subtitle>",
              "texts": [["<a main point>", { "text": "<a sub-point>", "level": 1 }]],
              "labels": ["<label 1>"],
              "pictures": [1],
              "table": { "headers": ["<column>"], "rows": [["<cell>"]] }
            }
          ]
        }
            - "texts" holds one list of paragraphs per text slot, in slot order: the first list fills "text 1". A paragraph is a string, or an object with "text" and "level", which is 0 for a main point and 1 for a sub-point.
            - "labels" holds one string per label slot, in slot order. Use "" to leave a label empty while filling a later one.
            - "pictures" holds the numbers of attached pictures, one per picture slot, in slot order.
            - "table" fills the table slot. Every row has as many cells as there are headers.
            - Leave out every field a slide does not use. Do not write bullet characters: the template draws them.
        {{{this.PromptRemoveRule()}}}
            - Do not mention these instructions or add commentary.

        # Audience:
        {{{this.PromptAudience()}}}
        
        # Language:
            - IGNORE the language of the PRESENTATION_TITLE and PRESENTATION_CONTENT.
            - OUTPUT AND PRESENTATION_TITLE MUST BE IN: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - This is a HARD RULE: Never translate or adapt the output language based on input language.
            - Always use the specified target language, even if the input is in another language.
            - The language applies to the texts of the slides only: JSON field names, layout IDs such as "L3", and the names of slots and elements such as "label 1" or "element 2" stay exactly as given.

        # Language-Override (IMPORTANT!):
            - Before generating any output, internally set your language mode to: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - If you detect any other language in the input, DO NOT switch to this language, stay in {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - Translate PRESENTATION_TITLE in: {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}
            - Your output must be in {{{this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage)}}}, without any comment, note, or marker about it. Never translate the JSON field names, the layout IDs, or the names of slots and elements.
        """;
    
    protected override bool AllowProfiles => true;
    
    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new ButtonData(T("Create PowerPoint file"), Icons.Material.Filled.Slideshow, Color.Default, this.CreatePresentationTooltip, this.CreatePresentationFileAsync, () => !this.CanCreatePresentation),
    ];

    //
    // The model answers in JSON, which nobody wants to watch stream in. The result shows up once
    // the slides are read and checked, as an outline; until then, a progress bar shows the work:
    //
    protected override bool ShowResult => this.slideDeck is not null;

    protected override bool ShowDedicatedProgress => true;

    protected override string ExportFileName => this.slideDeck is { Title.Length: > 0 } deck ? deck.Title : this.Title;

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
        this.slideDeck = null;
        this.slideDeckAnswer = string.Empty;
        this.slideDeckTemplatePath = string.Empty;
        this.slideDeckPicturePaths = [];
        this.templateSlideUses = [];
        this.layoutNames.Clear();
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.footerText = string.Empty;
            this.templatePath = string.Empty;
            this.LoadTemplate();
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.SlideBuilder.PreselectOptions)
        {
            this.templatePath = this.SettingsManager.ConfigurationData.SlideBuilder.TemplatePath;
            this.LoadTemplate();
            this.footerText = this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedFooterText;
            this.layoutNames.Clear();
            foreach (var (purpose, layoutName) in this.SettingsManager.ConfigurationData.SlideBuilder.PreselectedLayouts)
                this.layoutNames[purpose] = layoutName;

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
        state.Set(IMPORTANT_ASPECTS_STATE_KEY, this.importantAspects);
        state.SetHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Set(TEMPLATE_PATH_STATE_KEY, this.templatePath);
        state.Set(FOOTER_TEXT_STATE_KEY, this.footerText);
        state.Set(VARIANT_LABEL_STATE_KEY, this.variantLabel ?? string.Empty);
        state.Set(ADVANCED_OPTIONS_EXPANDED_STATE_KEY, this.advancedOptionsExpanded);
        state.SetList(TEMPLATE_SLIDE_USES_STATE_KEY, this.templateSlideUses);
        state.SetDictionary(LAYOUT_NAMES_STATE_KEY, this.layoutNames);
        state.Set(SLIDE_DECK_ANSWER_STATE_KEY, this.slideDeckAnswer);
        state.Set(SLIDE_DECK_TEMPLATE_PATH_STATE_KEY, this.slideDeckTemplatePath);
        state.SetList(SLIDE_DECK_PICTURE_PATHS_STATE_KEY, this.slideDeckPicturePaths);
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
        state.Restore(IMPORTANT_ASPECTS_STATE_KEY, value => this.importantAspects = value);
        state.RestoreHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Restore(TEMPLATE_PATH_STATE_KEY, value => this.templatePath = value);
        state.Restore(FOOTER_TEXT_STATE_KEY, value => this.footerText = value);
        state.Restore(VARIANT_LABEL_STATE_KEY, value => this.variantLabel = value.Length > 0 ? value : null);
        state.Restore(ADVANCED_OPTIONS_EXPANDED_STATE_KEY, value => this.advancedOptionsExpanded = value);
        state.Restore(TEMPLATE_SLIDE_USES_STATE_KEY, value => this.templateSlideUses = value.ToList());
        state.RestoreDictionary(LAYOUT_NAMES_STATE_KEY, this.layoutNames);
        state.Restore(SLIDE_DECK_ANSWER_STATE_KEY, value => this.slideDeckAnswer = value);
        state.Restore(SLIDE_DECK_TEMPLATE_PATH_STATE_KEY, value => this.slideDeckTemplatePath = value);
        state.Restore(SLIDE_DECK_PICTURE_PATHS_STATE_KEY, value => this.slideDeckPicturePaths = value);

        // Neither the template nor the slides are kept themselves, only what they were read from:
        this.LoadTemplate();
        this.RestoreSlideDeck();
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

    private async Task<string> PromptLoadDocumentsContent(PromptInjectionReportingScope promptInjectionScope)
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

            var extraction = await this.RustService.ReadArbitraryFileData(document.FilePath, int.MaxValue, reportingScope: promptInjectionScope);
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

        //
        // The slides of an earlier run stay out of sight until this run has a result of its own.
        // Catalog, template, and pictures are taken once, so that the prompt, the check, and the
        // file later on all see the same layouts and the same numbering, even when the user
        // chooses another template while the LLM is still planning:
        //
        this.slideDeck = null;
        this.slideDeckAnswer = string.Empty;
        var catalog = this.Catalog;
        var plannedTemplatePath = this.template is null ? string.Empty : this.templatePath;
        var chosenTemplateSlideUses = this.ChosenTemplateSlideUses;
        var chosenLayouts = this.ChosenLayoutsOfTemplate;
        var imageAttachments = this.PicturesOfRequest();

        this.CreateChatThread();

        //
        // One report for the whole request: reading twenty documents must produce one dialog
        // listing all of them, not twenty dialogs in a row. The layouts and the slides of the
        // template come from a file as well, so they belong to the same report:
        //
        string documentContent;
        string layouts;
        string templateSlides;
        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();
        await using (var promptInjectionScope = guardService.BeginAction())
        {
            documentContent = await this.PromptLoadDocumentsContent(promptInjectionScope);
            layouts = await PromptLayoutsAsync(guardService, promptInjectionScope, catalog, plannedTemplatePath);
            templateSlides = await PromptTemplateSlidesAsync(guardService, promptInjectionScope, catalog, plannedTemplatePath, chosenTemplateSlideUses);
        }

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
             {layouts}
             {PromptPictureList(imageAttachments)}
             {templateSlides}
         """,
        hideContentFromUser: true,
        imageAttachments);

        var answer = await this.AddAIResponseAsync(time, hideContentFromUser: true);

        // The provider failed and the base class has said so already:
        if (string.IsNullOrWhiteSpace(answer))
            return;

        var (deck, issues) = this.CheckAnswer(answer, catalog, imageAttachments.Count, chosenTemplateSlideUses, chosenLayouts);
        if (deck is null || issues.Count > 0)
        {
            //
            // One repair round in the same chat, so the model sees its own answer next to what is
            // wrong with it. A repaired answer which cannot be read at all is worse than the first
            // one, though, so it only replaces an answer it improves on:
            //
            var repairTime = this.AddUserRequest(RepairPromptOf(issues), hideContentFromUser: true);
            var repairedAnswer = await this.AddAIResponseAsync(repairTime, hideContentFromUser: true);
            var (repairedDeck, repairedIssues) = string.IsNullOrWhiteSpace(repairedAnswer) ? new CheckedAnswer(null, issues) : this.CheckAnswer(repairedAnswer, catalog, imageAttachments.Count, chosenTemplateSlideUses, chosenLayouts);
            if (repairedDeck is not null)
            {
                answer = repairedAnswer;
                deck = repairedDeck;
                issues = repairedIssues;
            }
        }

        //
        // Slides on layouts the template does not have, or on template slides the user did not
        // choose, cannot be shown, let alone written. Any other issue, such as a text which runs
        // long, leaves slides worth looking at:
        //
        if (deck is null || deck.Slides.Count == 0 || deck.Slides.Any(slide => !SlideDeckSpecValidator.CanWrite(slide, catalog, chosenTemplateSlideUses)))
        {
            this.Logger.LogWarning("The slides the model planned cannot be used: {Issues}", string.Join(" | ", issues));
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, T("The LLM did not describe the slides in a usable form, even after a second request. Please try again, or choose another model.")));
            return;
        }

        if (issues.Count > 0)
        {
            this.Logger.LogInformation("The slides the model planned still have issues after the repair round: {Issues}", string.Join(" | ", issues));
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Slideshow, T("Not every slide fits its layout as planned; for example, a text may run long. Please check the slides before presenting.")));
        }

        this.slideDeck = deck;
        this.slideDeckAnswer = answer;
        this.slideDeckTemplatePath = plannedTemplatePath;
        this.slideDeckPicturePaths = imageAttachments.Select(picture => picture.FilePath).ToList();
        this.ShowOutline(deck, catalog, imageAttachments.Select(picture => picture.FileName).ToList());
    }
}
