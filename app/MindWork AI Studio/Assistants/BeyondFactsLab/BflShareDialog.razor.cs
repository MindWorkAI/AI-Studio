using System.Globalization;
using System.Reflection;

using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Lets the person share a session of the BeyondFacts Lab with its research team: anonymized, checked in a preview
/// which the person may edit, exported as a file, and sent by mail from the mail program of the person.
/// </summary>
/// <remarks>
/// Nothing of the dialog is stored. The lab suggests the names it knows each time, and the person enters their own
/// name anew; the session itself stays as it is.
/// </remarks>
public partial class BflShareDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private AIGeneratedContentDisclosureService DisclosureService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflShareDialog> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the ID of the preparation to share.
    /// </summary>
    [Parameter]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets whether the lab asks right after the person finished the last step, so that it thanks them first.
    /// </summary>
    [Parameter]
    public bool AfterCompletion { get; set; }

    /// <summary>
    /// The version of AI Studio, which goes into the session, since the prompts of the lab change with it.
    /// </summary>
    private static readonly string APP_VERSION = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? "unknown";

    /// <summary>
    /// The format of every shared session, so that the research team gets all of them in the same one.
    /// </summary>
    /// <remarks>
    /// Markdown needs no Pandoc, so that everybody can share a session. The research team may ask for another format,
    /// but only one which AI Studio writes itself, see FileExportFormatExtensions.IsPlainText.
    /// </remarks>
    private const FileExportFormat EXPORT_FORMAT = FileExportFormat.MARKDOWN;

    /// <summary>
    /// Gets the culture of the active language plugin, for numbers.
    /// </summary>
    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Gets the link which opens the mail to the research team.
    /// </summary>
    private string MailUri { get; } = BflShareMail.CreateUri();

    /// <summary>
    /// What the session is put together from, or null when the preparation cannot be opened.
    /// </summary>
    private BflShareInput? source;

    /// <summary>
    /// Whether the dialog still reads the preparation.
    /// </summary>
    private bool isLoading = true;

    /// <summary>
    /// The name of the person, which the session should not contain.
    /// </summary>
    private string ownName = string.Empty;

    /// <summary>
    /// The names of other people, one per line, which the session should not contain.
    /// </summary>
    private string otherNames = string.Empty;

    /// <summary>
    /// Further terms, one per line, which the session should not contain.
    /// </summary>
    private string terms = string.Empty;

    /// <summary>
    /// Whether personal disclosures go into the session.
    /// </summary>
    private bool includePersonalDisclosures;

    /// <summary>
    /// The anonymized session as the person sees it in the preview, with what was replaced; null before the first
    /// preview.
    /// </summary>
    private BflAnonymization? preview;

    /// <summary>
    /// Whether the person changed the preview, which a new preview would discard.
    /// </summary>
    private bool previewEdited;

    /// <summary>
    /// Whether an export runs.
    /// </summary>
    private bool isExporting;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        try
        {
            var forms = await this.Store.LoadFormsAsync(this.ProjectId);
            var workStates = await this.Store.LoadWorkStatesAsync(this.ProjectId);
            var results = await this.Store.LoadResultsAsync(this.ProjectId);
            var research = await this.Store.LoadAllResearchAsync(this.ProjectId);
            var materials = (await this.Store.ListMaterialsAsync(this.ProjectId)).Select(entry => entry.Material).OfType<BflMaterial>().ToList();
            var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>();
            foreach (var step in BflStep.All)
            {
                if ((await this.ConversationService.GetAsync(new BflStepThreadLocation(this.ProjectId, step))).Thread is { } thread)
                    conversations[step] = BflReflectionFolder.MessagesOf(thread);
            }

            var researchTopics = research.Where(entry => !string.IsNullOrWhiteSpace(entry.Value.Text)).Select(entry => entry.Key).ToList();
            this.source = new BflShareInput(forms, workStates, results, conversations, materials, researchTopics, false, APP_VERSION, DateTimeOffset.UtcNow, Culture);
            this.otherNames = string.Join('\n', BflShareNames.Suggest(forms, workStates, research.GetValueOrDefault(BflResearchTopic.PARTICIPANTS)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not read the preparation '{ProjectId}' of the BeyondFacts Lab to share it.", this.ProjectId);
            this.source = null;
        }
        finally
        {
            this.isLoading = false;
        }
    }

    /// <summary>
    /// Puts the session together and anonymizes it with the names and terms the person listed; asks first when that
    /// would discard changes of the person in the preview.
    /// </summary>
    private async Task CreatePreviewAsync()
    {
        if (this.source is null)
            return;

        if (this.previewEdited)
        {
            var parameters = new DialogParameters<ConfirmDialog>
            {
                { dialog => dialog.Message, TA("Create the preview anew? Your changes in the preview will be lost.") },
            };

            var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Create preview anew"), parameters, DialogOptions.FULLSCREEN);
            var answer = await reference.Result;
            if (answer is null || answer.Canceled)
                return;
        }

        var session = BflShareModelBuilder.Build(this.source with { IncludePersonalDisclosures = this.includePersonalDisclosures, SharedAtUtc = DateTimeOffset.UtcNow });
        this.preview = this.Anonymize(session);
        this.previewEdited = false;
    }

    /// <summary>
    /// Anonymizes the preview once more after the person changed it, so that a name the person typed goes as well.
    /// </summary>
    /// <param name="text">The preview as the person changed it.</param>
    private void PreviewChanged(string? text)
    {
        if (this.preview is null)
            return;

        this.preview = this.preview.CombinedWith(this.Anonymize(text ?? string.Empty));
        this.previewEdited = true;
    }

    /// <summary>
    /// Exports the preview in the one format the research team gets, anonymized once more with the names and terms as
    /// they stand now, and with the notice that AI generated it when the person or the organization switched that on.
    /// </summary>
    private async Task ExportAsync()
    {
        if (this.preview is null)
            return;

        this.preview = this.preview.CombinedWith(this.Anonymize(this.preview.Text));
        var fileName = $"{T("BeyondFacts Lab")} - {T("Shared session")}";
        this.isExporting = true;
        try
        {
            await PlainFileExport.ToFile(this.RustService, T("Share with the research team"), EXPORT_FORMAT, this.DisclosureService.AddDisclosureToMarkdown(this.preview.Text), fileName);
        }
        finally
        {
            this.isExporting = false;
        }
    }

    /// <summary>
    /// Anonymizes a text with the names and terms the person listed, the own name first.
    /// </summary>
    /// <param name="text">The text.</param>
    private BflAnonymization Anonymize(string text) => BflAnonymizer.Anonymize(text, [this.ownName, .. LinesOf(this.otherNames)], LinesOf(this.terms));

    /// <summary>
    /// Gets the entries of a field with one entry per line.
    /// </summary>
    /// <param name="text">The text of the field.</param>
    private static IReadOnlyList<string> LinesOf(string text) => text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private void Close() => this.MudDialog.Close(DialogResult.Ok(true));
}