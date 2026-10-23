using System.Globalization;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PowerPoint;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// The part of the Slide Planner which works with PowerPoint: the template, the slides the model
/// plans for its layouts, and the file written from them.
/// </summary>
public partial class SlideAssistant
{
    /// <summary>
    /// The layouts of a plain presentation, which the model plans with when no template was chosen.
    /// </summary>
    private static readonly PresentationTemplate DEFAULT_CATALOG = PresentationTemplate.CreateDefault();

    /// <summary>
    /// How many issues the repair request names at most.
    /// </summary>
    /// <remarks>
    /// A model handed fifty issues fixes the first few and loses track of the rest; a dozen names
    /// what is wrong in kind, and the model finds the like on its own.
    /// </remarks>
    private const int MAX_REPORTED_ISSUES = 12;

    private string templatePath = string.Empty;
    private string footerText = string.Empty;
    private string? variantLabel;
    private PresentationTemplate? template;
    private SlideDeckSpec? slideDeck;
    private bool isWritingPresentation;

    /// <summary>
    /// Whether the options of the template are open: its color variant, the footer, and its slides.
    /// </summary>
    private bool advancedOptionsExpanded;

    /// <summary>
    /// What the user chose for each slide of the template, by its number.
    /// </summary>
    private List<TemplateSlideUse> templateSlideUses = [];

    /// <summary>
    /// The answer the slide deck was read from, which the session keeps to restore the deck.
    /// </summary>
    private string slideDeckAnswer = string.Empty;

    /// <summary>
    /// The template the slide deck was planned for; empty when it was planned without one.
    /// </summary>
    private string slideDeckTemplatePath = string.Empty;

    /// <summary>
    /// The pictures the model numbered while planning, in its order.
    /// </summary>
    private List<string> slideDeckPicturePaths = [];

    private static readonly AssistantSessionStateKey<string> TEMPLATE_PATH_STATE_KEY = new(nameof(templatePath));
    private static readonly AssistantSessionStateKey<string> FOOTER_TEXT_STATE_KEY = new(nameof(footerText));
    private static readonly AssistantSessionStateKey<string> VARIANT_LABEL_STATE_KEY = new(nameof(variantLabel));
    private static readonly AssistantSessionStateKey<bool> ADVANCED_OPTIONS_EXPANDED_STATE_KEY = new(nameof(advancedOptionsExpanded));
    private static readonly AssistantSessionStateKey<List<TemplateSlideUse>> TEMPLATE_SLIDE_USES_STATE_KEY = new(nameof(templateSlideUses));
    private static readonly AssistantSessionStateKey<string> SLIDE_DECK_ANSWER_STATE_KEY = new(nameof(slideDeckAnswer));
    private static readonly AssistantSessionStateKey<string> SLIDE_DECK_TEMPLATE_PATH_STATE_KEY = new(nameof(slideDeckTemplatePath));
    private static readonly AssistantSessionStateKey<List<string>> SLIDE_DECK_PICTURE_PATHS_STATE_KEY = new(nameof(slideDeckPicturePaths));

    /// <summary>
    /// The layouts the model plans with: those of the chosen template, or those of a plain presentation.
    /// </summary>
    private PresentationTemplate Catalog => this.template ?? DEFAULT_CATALOG;

    private bool IsTemplateLocked => ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.TemplatePath, out var meta) && meta.IsLocked;

    /// <summary>
    /// What the user chose for the slides of the template, as the model and the check of its answer see it; empty without a template.
    /// </summary>
    private IReadOnlyList<TemplateSlideUse> ChosenTemplateSlideUses => this.template is null ? [] : this.templateSlideUses.ToList();

    private bool HasChosenTemplateSlides => this.template is not null && this.templateSlideUses.Any(use => use is not TemplateSlideUse.OMIT);

    /// <summary>
    /// How many slides the user asked for, or 0 when the model may choose.
    /// </summary>
    /// <remarks>
    /// A number of slides wins over a duration. A duration means one slide per one and a half
    /// minutes, as the form has always counted it.
    /// </remarks>
    private int ExpectedSlideCount => this.numberOfSheets > 0
        ? this.numberOfSheets
        : (int)Math.Round(this.timeSpecification / 1.5);

    private bool PlannedForCurrentTemplate => this.template is not null && this.slideDeckTemplatePath.Length > 0 && string.Equals(this.slideDeckTemplatePath, this.templatePath, StringComparison.Ordinal);

    private bool CanCreatePresentation => this.slideDeck is not null && this.PlannedForCurrentTemplate && !this.isWritingPresentation && !this.IsProcessing;

    private string CreatePresentationTooltip
    {
        get
        {
            if (this.slideDeck is null)
                return T("Please plan the slides first.");

            if (this.template is null)
                return T("Please choose a PowerPoint template to create a file from the slides.");

            if (!this.PlannedForCurrentTemplate)
                return T("The slides were planned for another template. Please plan them again.");

            return T("Creates a PowerPoint file in the design of your template.");
        }
    }

    /// <summary>
    /// Reads the layouts of the chosen template.
    /// </summary>
    /// <remarks>
    /// A template which cannot be read leaves no template at all. The form then says why, and
    /// nothing is planned for layouts nobody can write.
    /// </remarks>
    private void LoadTemplate()
    {
        this.template = null;
        if (string.IsNullOrWhiteSpace(this.templatePath) || !File.Exists(this.templatePath))
            return;

        try
        {
            using var stream = new FileStream(this.templatePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            this.template = PresentationTemplate.Read(stream);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "The PowerPoint template '{TemplatePath}' cannot be read.", this.templatePath);
            return;
        }

        // A variant of another template means nothing for this one, nor do choices for its slides:
        if (this.variantLabel is null || !this.template.VariantLabels.Contains(this.variantLabel))
            this.variantLabel = this.template.VariantLabels.FirstOrDefault();

        if (this.templateSlideUses.Count != this.template.Slides.Count)
            this.templateSlideUses = DefaultTemplateSlideUsesOf(this.template);
    }

    private async Task OnTemplateChanged(string path)
    {
        this.templatePath = path;
        this.templateSlideUses = [];
        this.LoadTemplate();
        if (this.Form is not null)
            await this.Form.Validate();
    }

    /// <summary>
    /// What the presentation does with the slides of a template unless the user says otherwise.
    /// </summary>
    /// <remarks>
    /// The LLM edits every slide it can, so that a presentation used as a template becomes the next
    /// one in its structure. A slide whose layout has no slots for content is kept as it is. Samples
    /// and hints of a template are for the user to leave out.
    /// </remarks>
    private static List<TemplateSlideUse> DefaultTemplateSlideUsesOf(PresentationTemplate template) => template.Slides
        .Select(slide => slide.FamilyId is null ? TemplateSlideUse.KEEP : TemplateSlideUse.EDIT)
        .ToList();

    private TemplateSlideUse TemplateSlideUseOf(int number) => this.templateSlideUses.UseOf(number);

    private void SetTemplateSlideUse(int number, TemplateSlideUse use)
    {
        if (number >= 1 && number <= this.templateSlideUses.Count)
            this.templateSlideUses[number - 1] = use;
    }

    private string? ValidateTemplateSlideUse(TemplateSlide slide, TemplateSlideUse use)
    {
        if (use is TemplateSlideUse.EDIT && slide.FamilyId is null)
            return T("The LLM cannot edit this slide, because its layout has no placeholders for content. Please keep it unchanged or leave it out.");

        return null;
    }

    /// <summary>
    /// How the form names a slide of the template: by its number, and what is special about it.
    /// </summary>
    private string TemplateSlideLabelOf(TemplateSlide slide)
    {
        var label = string.Format(T("Slide {0}"), slide.Number);
        if (slide.IsHidden)
            label = $"{label} ({T("hidden")})";

        return label;
    }

    private async Task UseNoTemplate() => await this.OnTemplateChanged(string.Empty);

    private string? ValidateTemplate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!File.Exists(path))
            return T("The template file does not exist.");

        if (this.template is null)
            return T("The template cannot be read. Please choose a PowerPoint presentation or template (.pptx, .potx).");

        if (this.template.Families.Count == 0)
            return T("The template has no layouts with room for content.");

        return null;
    }

    /// <summary>
    /// The pictures of the request, in the order the model numbers them.
    /// </summary>
    private List<FileAttachment> PicturesOfRequest() => this.loadedDocumentPaths.Where(n => n is { Exists: true, IsImage: true }).ToList();

    private string PromptPictures() => this.PicturesOfRequest().Count == 0
        ? "    - No pictures are attached. Leave out \"pictures\" on every slide."
        : "    - PICTURES lists the attached pictures by their numbers. Where a picture supports the content, place it by its number on a slide with a picture slot, each picture at most once.";

    /// <summary>
    /// The attached pictures by their numbers, for the user's request.
    /// </summary>
    /// <remarks>
    /// The names are those of the user's files, so they go with the request, like the paths of the
    /// attached documents, rather than into the system prompt.
    /// </remarks>
    /// <param name="pictures">The pictures taken at the start of the request, in the order the model numbers them.</param>
    /// <returns>The section of the request; empty without pictures.</returns>
    private static string PromptPictureList(IReadOnlyList<FileAttachment> pictures)
    {
        if (pictures.Count == 0)
            return string.Empty;

        var list = string.Join(Environment.NewLine, pictures.Select((picture, index) => $"- Picture {index + 1}: {picture.FileName}"));
        return $"""

                # PICTURES
                {list}
                """;
    }

    private string PromptSlideCount()
    {
        var expectedSlideCount = this.ExpectedSlideCount;
        if (expectedSlideCount <= 0)
            return "    - Choose as many slides as the content needs.";

        return this.HasChosenTemplateSlides
            ? $"    - The presentation has exactly {expectedSlideCount} slides, the title slide and the template slides included."
            : $"    - The presentation has exactly {expectedSlideCount} slides, the title slide included.";
    }

    /// <summary>
    /// The rules for the slides of the template the user chose; empty when the user chose none.
    /// </summary>
    /// <remarks>
    /// The slides themselves come with the user's request, as TEMPLATE_SLIDES, since their texts are
    /// content of a file rather than instructions.
    /// </remarks>
    private string PromptTemplateSlides()
    {
        if (!this.HasChosenTemplateSlides)
            return string.Empty;

        return """

                 # Template slides
                 The user chose slides of the template to use in the presentation. TEMPLATE_SLIDES lists them in their order, each one either to keep unchanged or to edit.
                     - Write a slide to keep as { "template_slide": <its number> } and nothing else. Add "starts_chapter": true where it opens a chapter.
                     - Write a slide to edit with "template_slide": <its number> and its new content in the usual fields, which fill the slots its entry names. Leave out "layout": the slide keeps its own. A slot you neither fill nor remove keeps the content of the template slide, and so does every element you do not remove.
                     - Where an entry gives a slot its place and size "on this slide", the slide moved or resized it, e.g., to make room for a picture of its own, or carries it as a copy of another slot, e.g., a further heading with its text. Fill such a slot like any other, and stay within the lines and characters stated there rather than those of the layout.
                     - An entry to edit also lists the elements the slide carries besides its slots, e.g., pictures, text boxes, charts, or tables of its own, with their place and the slots they overlap. List what the slide no longer shows in "remove", by the names the entry uses, e.g., "remove": ["element 2", "label 1"].
                     - Remove what does not fit the new content: samples and hints of the template, e.g., placeholder pictures, sample texts such as "Lorem ipsum", or a request to name the source of a picture; old content of a slot which no longer fits; and every element which overlaps a slot you fill, unless it covers most of the slide as a background. Keep what still belongs to the slide, e.g., a logo or a decoration. Never fill and remove the same slot, and never remove the title.
                     - Use every listed slide exactly once, in the listed order. Add new slides before, between, and after them, wherever the content calls for it.
                     - Do not repeat on a new slide what a kept slide says already.
                     - Example: "slides": [{ "template_slide": 1 }, { "layout": "L3", "title": "..." }, { "template_slide": 3, "title": "...", "texts": [["..."]], "remove": ["element 1"] }]
                 """;
    }

    /// <summary>
    /// The rule for the field listing what an edited template slide removes; empty when the user
    /// edits no template slide, so that the model does not hear of the field at all.
    /// </summary>
    private string PromptRemoveRule() => this.template is not null && this.templateSlideUses.Any(use => use is TemplateSlideUse.EDIT)
        ? """
               - "remove" lists the slots and elements a template slide to edit no longer shows, by the names TEMPLATE_SLIDES uses. Leave it out on every other slide.
           """
        : string.Empty;

    /// <summary>
    /// The layouts the model plans with, for the user's request.
    /// </summary>
    /// <remarks>
    /// The catalog names the layouts as the author of the template named them. These names come from
    /// a file, which may carry instructions of somebody else, so the catalog goes with the request
    /// rather than into the system prompt, and the layouts of a template pass the prompt injection
    /// filter like its slides. The layouts of a plain presentation come from the code.
    /// </remarks>
    /// <param name="guardService">The prompt injection filter.</param>
    /// <param name="promptInjectionScope">The action of the request, which reports what the filter removed.</param>
    /// <param name="catalog">The layouts taken at the start of the request.</param>
    /// <param name="templatePath">The template the layouts were read from; empty for the layouts of a plain presentation.</param>
    /// <returns>The section of the request.</returns>
    private static async Task<string> PromptLayoutsAsync(PromptInjectionGuardService guardService, PromptInjectionReportingScope promptInjectionScope, PresentationTemplate catalog, string templatePath)
    {
        var layouts = catalog.ToCatalogPrompt();
        if (templatePath.Length > 0)
            layouts = await guardService.SanitizeAsync(layouts, PromptInjectionSource.FileContent(templatePath), promptInjectionScope);

        return $"""

                # LAYOUTS
                {layouts}
                """;
    }

    /// <summary>
    /// The slides of the template the user chose, with what they hold, for the user's request.
    /// </summary>
    /// <remarks>
    /// Their texts come from a file, which may carry instructions of somebody else, so they pass the
    /// prompt injection filter like every attached document.
    /// </remarks>
    /// <param name="guardService">The prompt injection filter.</param>
    /// <param name="promptInjectionScope">The action of the request, which reports what the filter removed.</param>
    /// <param name="catalog">The layouts and slides taken at the start of the request.</param>
    /// <param name="templatePath">The template they were read from; empty without a template.</param>
    /// <param name="templateSlideUses">What the user chose for each slide of the template.</param>
    /// <returns>The section of the request; empty when the user chose no slide.</returns>
    private static async Task<string> PromptTemplateSlidesAsync(PromptInjectionGuardService guardService, PromptInjectionReportingScope promptInjectionScope, PresentationTemplate catalog, string templatePath, IReadOnlyList<TemplateSlideUse> templateSlideUses)
    {
        if (templatePath.Length == 0 || catalog.ToTemplateSlidesPrompt(templateSlideUses) is not { Length: > 0 } templateSlides)
            return string.Empty;

        var sanitizedTemplateSlides = await guardService.SanitizeAsync(templateSlides, PromptInjectionSource.FileContent(templatePath), promptInjectionScope);
        return $"""

                # TEMPLATE_SLIDES
                {sanitizedTemplateSlides}
                """;
    }

    private string PromptBulletPoints()
    {
        var count = this.numberOfBulletPoints > 0
            ? $"    - Every text slot holding a list has exactly {this.numberOfBulletPoints} main points, as far as its space allows."
            : "    - Choose between 1 and 7 main points per text slot, as its space allows.";

        return $"""
                {count}
                    - Every point is short: at most 12 words, clear, and directly related to the slide title.
                """;
    }

    /// <summary>
    /// Reads the slide deck out of an answer and checks it against the layouts it was planned for.
    /// </summary>
    /// <returns>The deck, or null when the answer cannot be read; and the issues, written for the model.</returns>
    private (SlideDeckSpec? Deck, IReadOnlyList<string> Issues) CheckAnswer(string answer, PresentationTemplate catalog, int pictureCount, IReadOnlyList<TemplateSlideUse> templateSlideUses)
    {
        if (!SlideDeckSpec.TryParse(answer, out var deck, out var issue))
            return (null, [issue]);

        return (deck, SlideDeckSpecValidator.Validate(deck, catalog, pictureCount, this.ExpectedSlideCount, templateSlideUses));
    }

    private static string RepairPromptOf(IReadOnlyList<string> issues)
    {
        var listedIssues = string.Join(Environment.NewLine, issues.Take(MAX_REPORTED_ISSUES).Select(issue => $"- {issue}"));
        return $"""
                Your JSON does not fit the layouts yet:
                {listedIssues}

                Answer with the complete corrected JSON object and nothing else.
                """;
    }

    /// <summary>
    /// Shows the slide deck as an outline, which is the visible result of the assistant.
    /// </summary>
    /// <remarks>
    /// The outline becomes the last block of the chat, so copying the result, exporting it, and
    /// sending it to a chat all work with the outline rather than with the JSON behind it.
    /// </remarks>
    private void ShowOutline(SlideDeckSpec deck, PresentationTemplate catalog, IReadOnlyList<string> pictureNames)
    {
        var outline = new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            HideFromUser = false,
            Content = new ContentText { Text = deck.ToMarkdownOutline(catalog, pictureNames) },
        };

        this.ChatThread?.Blocks.Add(outline);
        this.ResultingContentBlock = outline;
    }

    private void RestoreSlideDeck() => this.slideDeck = this.slideDeckAnswer.Length > 0 && SlideDeckSpec.TryParse(this.slideDeckAnswer, out var deck, out _) ? deck : null;

    private async Task CreatePresentationFileAsync()
    {
        if (!this.CanCreatePresentation || this.slideDeck is not { } deck)
            return;

        var fileName = FileExportFormatExtensions.ToFileNameFragment(deck.Title);
        var response = await this.RustService.SaveFile(T("Save the presentation"), [FileTypes.POWER_POINT_PRESENTATION], $"{(fileName.Length == 0 ? "presentation" : fileName)}.pptx");
        if (response.UserCancelled)
            return;

        //
        // The presentation comes about in a copy of the template next to the target, which takes its
        // place once it is complete. A failure therefore leaves no half-written file behind, and
        // neither the template nor the presentation has to be held in memory:
        //
        var targetPath = response.SaveFilePath;
        var temporaryPath = Path.Join(Path.GetDirectoryName(targetPath), $".{Path.GetFileName(targetPath)}-{Guid.NewGuid():N}.tmp");
        this.isWritingPresentation = true;
        this.StateHasChanged();
        try
        {
            await using (var templateFile = new FileStream(this.slideDeckTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var copy = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await templateFile.CopyToAsync(copy);

            PresentationWriteResult result;
            await using (var presentationFile = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                //
                // Read once more rather than reused: the template may have changed since the slides
                // were planned, and the writer has to see the layouts of the very file it edits.
                //
                var currentTemplate = PresentationTemplate.Read(presentationFile);
                presentationFile.Position = 0;

                var options = new PresentationWriteOptions(this.variantLabel, this.footerText.Trim());
                result = await PresentationWriter.WriteAsync(presentationFile, currentTemplate, deck, options, new RustPictureSource(this.RustService, this.slideDeckPicturePaths), CancellationToken.None);
            }

            File.Move(temporaryPath, targetPath, true);
            if (result.SkippedPictures.Count > 0)
            {
                var names = result.SkippedPictures.Select(number => number >= 1 && number <= this.slideDeckPicturePaths.Count ? Path.GetFileName(this.slideDeckPicturePaths[number - 1]) : number.ToString(CultureInfo.InvariantCulture));
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.HideImage, string.Format(T("These pictures could not be placed, because PowerPoint cannot show their format or they cannot be read: {0}"), string.Join(", ", names))));
            }

            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Slideshow, T("The PowerPoint file was created.")));
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Creating the PowerPoint file '{FilePath}' failed.", targetPath);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, T("The PowerPoint file could not be created. Please check that the template can be read and that the target file is not open in another program.")));
        }
        finally
        {
            this.isWritingPresentation = false;
            this.StateHasChanged();
            this.DeleteTemporaryPresentation(temporaryPath);
        }
    }

    /// <summary>
    /// Removes the copy of the template which a failed attempt left next to the target.
    /// </summary>
    /// <remarks>
    /// After a success, the copy has become the presentation, and there is nothing left to remove.
    /// Left behind, it would pile up in the folder of the user with every failed attempt.
    /// </remarks>
    private void DeleteTemporaryPresentation(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "The temporary file '{TemporaryPath}' cannot be deleted.", temporaryPath);
        }
    }
}