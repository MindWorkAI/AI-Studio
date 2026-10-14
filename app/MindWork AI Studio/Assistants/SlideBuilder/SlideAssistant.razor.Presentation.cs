using System.Globalization;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PowerPoint;
using AIStudio.Tools.Rust;

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
    private static readonly AssistantSessionStateKey<string> SLIDE_DECK_ANSWER_STATE_KEY = new(nameof(slideDeckAnswer));
    private static readonly AssistantSessionStateKey<string> SLIDE_DECK_TEMPLATE_PATH_STATE_KEY = new(nameof(slideDeckTemplatePath));
    private static readonly AssistantSessionStateKey<List<string>> SLIDE_DECK_PICTURE_PATHS_STATE_KEY = new(nameof(slideDeckPicturePaths));

    /// <summary>
    /// The layouts the model plans with: those of the chosen template, or those of a plain presentation.
    /// </summary>
    private PresentationTemplate Catalog => this.template ?? DEFAULT_CATALOG;

    private bool IsTemplateLocked => ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.TemplatePath, out var meta) && meta.IsLocked;

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

        // A variant of another template means nothing for this one:
        if (this.variantLabel is null || !this.template.VariantLabels.Contains(this.variantLabel))
            this.variantLabel = this.template.VariantLabels.FirstOrDefault();
    }

    private async Task OnTemplateChanged(string path)
    {
        this.templatePath = path;
        this.LoadTemplate();
        if (this.Form is not null)
            await this.Form.Validate();
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

    private string PromptPictures()
    {
        var pictures = this.PicturesOfRequest();
        if (pictures.Count == 0)
            return "    - No pictures are attached. Leave out \"pictures\" on every slide.";

        var list = string.Join(Environment.NewLine, pictures.Select((picture, index) => $"    - Picture {index + 1}: {picture.FileName}"));
        return $"""
                    - The following pictures are attached. Where a picture supports the content, place it by its number on a slide with a picture slot, each picture at most once:
                {list}
                """;
    }

    private string PromptSlideCount()
    {
        var expectedSlideCount = this.ExpectedSlideCount;
        return expectedSlideCount > 0
            ? $"    - The presentation has exactly {expectedSlideCount} slides, the title slide included."
            : "    - Choose as many slides as the content needs.";
    }

    /// <summary>
    /// The texts of the imprint for the model to translate, in German as the corporate template states them.
    /// </summary>
    private static string PromptImprint() => ImprintTexts.GERMAN.ToPromptJson();

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
    private (SlideDeckSpec? Deck, IReadOnlyList<string> Issues) CheckAnswer(string answer, PresentationTemplate catalog, int pictureCount)
    {
        if (!SlideDeckSpec.TryParse(answer, out var deck, out var issue))
            return (null, [issue]);

        return (deck, SlideDeckSpecValidator.Validate(deck, catalog, pictureCount, this.ExpectedSlideCount));
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

        this.isWritingPresentation = true;
        this.StateHasChanged();
        try
        {
            using var presentation = new MemoryStream();
            PresentationWriteResult result;
            await using (var templateFile = new FileStream(this.slideDeckTemplatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                //
                // Read once more rather than reused: the template may have changed since the slides
                // were planned, and the writer has to see the layouts of the very file it copies.
                //
                var currentTemplate = PresentationTemplate.Read(templateFile);
                templateFile.Position = 0;

                var options = new PresentationWriteOptions(this.variantLabel, this.footerText.Trim());
                result = await PresentationWriter.WriteAsync(templateFile, currentTemplate, deck, options, new RustPictureSource(this.RustService, this.slideDeckPicturePaths), presentation, CancellationToken.None);
            }

            // Written in one go, so a failure above leaves no half-written file behind:
            presentation.Position = 0;
            await using (var target = new FileStream(response.SaveFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                await presentation.CopyToAsync(target);

            if (result.SkippedPictures.Count > 0)
            {
                var names = result.SkippedPictures.Select(number => number >= 1 && number <= this.slideDeckPicturePaths.Count ? Path.GetFileName(this.slideDeckPicturePaths[number - 1]) : number.ToString(CultureInfo.InvariantCulture));
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.HideImage, string.Format(T("These pictures could not be placed, because PowerPoint cannot show their format or they cannot be read: {0}"), string.Join(", ", names))));
            }

            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Slideshow, T("The PowerPoint file was created.")));
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Creating the PowerPoint file '{FilePath}' failed.", response.SaveFilePath);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Slideshow, T("The PowerPoint file could not be created. Please check that the template can be read and that the target file is not open in another program.")));
        }
        finally
        {
            this.isWritingPresentation = false;
            this.StateHasChanged();
        }
    }
}