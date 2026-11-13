using System.Globalization;

using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The results of a preparation in the last step of the BeyondFacts Lab: what the person takes away, each with its
/// draft, its confirmed version, and its export.
/// </summary>
/// <remarks>
/// The component loads and stores the results itself; the page keeps the work states, which tell whether a result is
/// outdated. The model writes in BflResultGenerator, so that the work goes on while the person looks elsewhere; the
/// component reads the results again whenever the generator says one changed. Only a confirmed result can be exported.
/// </remarks>
public partial class BflResults : MSGComponentBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private BflResultGenerator Generator { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailability { get; init; } = null!;

    [Inject]
    private AIGeneratedContentDisclosureService DisclosureService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflResults> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the ID of the preparation whose results the component shows.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the name of the preparation, for the reflection folder and the names of exported files.
    /// </summary>
    [Parameter, EditorRequired]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the work states of the preparation, by their steps.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflWorkState> WorkStates { get; set; } = new Dictionary<BflStep, BflWorkState>();

    /// <summary>
    /// Gets or sets the forms of the preparation, by their steps, which the reflection folder needs to tell where a
    /// work state stands.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflStepFormData> Forms { get; set; } = new Dictionary<BflStep, BflStepFormData>();

    /// <summary>
    /// Gets or sets the callback raised after a result was stored, with the preparation and its new time of change.
    /// </summary>
    [Parameter]
    public EventCallback<BflProject> ResultStored { get; set; }

    /// <summary>
    /// Gets or sets the provider which writes the results.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider cannot write, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// The results this version can open, by their kinds.
    /// </summary>
    private Dictionary<BflResultKind, BflResult> results = [];

    /// <summary>
    /// The preparation whose results were loaded, to load again when the page shows another one.
    /// </summary>
    private Guid loadedProjectId;

    /// <summary>
    /// The result whose panel is open, or null while all are closed. Only one is open at a time.
    /// </summary>
    private BflResultKind? expandedKind = BflResultKind.All[0];

    /// <summary>
    /// The results whose draft the person edits as text instead of reading its preview.
    /// </summary>
    private readonly HashSet<BflResultKind> editing = [];

    /// <summary>
    /// The results the component stores or puts together right now.
    /// </summary>
    private readonly HashSet<BflResultKind> saving = [];

    /// <summary>
    /// Whether the reflection folder takes up personal disclosures.
    /// </summary>
    private bool includePersonalDisclosures;

    /// <summary>
    /// Whether the reflection folder takes up the conversations of the steps as an appendix.
    /// </summary>
    private bool includeConversations;

    /// <summary>
    /// Gets the steps without a confirmed work state, in their order.
    /// </summary>
    private IReadOnlyList<BflStep> MissingSteps => [.. BflStep.All.Where(step => this.WorkStates.GetValueOrDefault(step)?.Confirmed is null)];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.Generator.ResultChanged += this.OnResultChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.loadedProjectId == this.ProjectId)
            return;

        this.loadedProjectId = this.ProjectId;
        this.editing.Clear();
        this.results = [];
        await this.LoadAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.Generator.ResultChanged -= this.OnResultChangedAsync;
        base.DisposeResources();
    }

    private bool IsGenerating(BflResultKind kind) => this.Generator.IsRunning(new BflResultLocation(this.ProjectId, kind));

    private bool IsBusy(BflResultKind kind) => this.saving.Contains(kind) || this.IsGenerating(kind);

    /// <summary>
    /// Gets the results which "Create all materials in parallel" creates: every one which can be opened and is idle.
    /// </summary>
    private IReadOnlyList<BflResultKind> CreatableKinds => [.. BflResultKind.All.Where(kind => this.results.ContainsKey(kind) && !this.IsBusy(kind))];

    private bool CanCreateAll => this.ProviderIssue is null && this.CreatableKinds.Count > 0;

    private bool AnyCreated => this.results.Values.Any(result => result.Draft is not null || result.Confirmed is not null);

    /// <summary>
    /// Tells where a result stands, for the header of its panel.
    /// </summary>
    /// <param name="kind">The kind of result.</param>
    private BflResultStatus StatusOf(BflResultKind kind)
    {
        if (this.IsGenerating(kind))
            return BflResultStatus.WRITING;

        return this.results.GetValueOrDefault(kind) switch
        {
            null => BflResultStatus.UNREADABLE,
            { Draft: not null } => BflResultStatus.DRAFT,
            { Confirmed: { } confirmed } => BflStaleness.IsOutdated(confirmed, this.WorkStates) ? BflResultStatus.OUTDATED : BflResultStatus.CONFIRMED,
            _ => BflResultStatus.NOT_CREATED
        };
    }

    private static string StatusIcon(BflResultStatus status) => status switch
    {
        BflResultStatus.UNREADABLE => AppIcons.ERROR,
        BflResultStatus.WRITING => AppIcons.PENDING,
        BflResultStatus.DRAFT => AppIcons.DRAFT,
        BflResultStatus.CONFIRMED => AppIcons.SUCCESS,
        BflResultStatus.OUTDATED => AppIcons.OUTDATED,
        _ => AppIcons.NOT_CREATED
    };

    private static Color StatusColor(BflResultStatus status) => status switch
    {
        BflResultStatus.UNREADABLE => Color.Error,
        BflResultStatus.WRITING => Color.Primary,
        BflResultStatus.DRAFT => Color.Info,
        BflResultStatus.CONFIRMED => Color.Success,
        BflResultStatus.OUTDATED => Color.Warning,
        _ => Color.Default
    };

    private string StatusText(BflResultStatus status) => status switch
    {
        BflResultStatus.UNREADABLE => T("Cannot be opened"),
        BflResultStatus.WRITING => T("Being written"),
        BflResultStatus.DRAFT => T("Draft"),
        BflResultStatus.CONFIRMED => T("Confirmed"),
        BflResultStatus.OUTDATED => T("Outdated"),
        _ => T("Not created yet")
    };

    /// <summary>
    /// Opens the panel of a result and closes the others, or closes it.
    /// </summary>
    /// <param name="kind">The kind of result.</param>
    /// <param name="expanded">Whether the panel is open now.</param>
    private void SetExpanded(BflResultKind kind, bool expanded)
    {
        if (expanded)
            this.expandedKind = kind;
        else if (this.expandedKind == kind)
            this.expandedKind = null;
    }

    /// <summary>
    /// Reads the results of the preparation again once the generator started, ended, or stored a draft.
    /// </summary>
    /// <param name="location">The result which changed.</param>
    private Task OnResultChangedAsync(BflResultLocation location)
    {
        if (location.ProjectId != this.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            await this.LoadAsync();
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Reads the results of the preparation shown.
    /// </summary>
    private async Task LoadAsync()
    {
        var projectId = this.ProjectId;
        var loaded = await this.Store.LoadResultsAsync(projectId);

        // The page may have shown another preparation meanwhile:
        if (projectId == this.ProjectId)
            this.results = new(loaded);
    }

    /// <summary>
    /// Lets the model write a result, or puts the reflection folder together.
    /// </summary>
    /// <param name="kind">The kind of result.</param>
    private async Task CreateAsync(BflResultKind kind)
    {
        this.editing.Remove(kind);
        if (kind.IsWrittenByModel)
        {
            if (this.ProviderIssue is null)
                await this.Generator.GenerateAsync(new BflResultLocation(this.ProjectId, kind), this.Provider);

            return;
        }

        this.saving.Add(kind);
        try
        {
            await this.Generator.AssembleReflectionFolderAsync(this.ProjectId, this.ProjectName, this.Forms, this.includePersonalDisclosures, this.includeConversations);
        }
        finally
        {
            this.saving.Remove(kind);
        }
    }

    /// <summary>
    /// Creates every idle result at once: the model writes the four of them in parallel, while AI Studio puts the
    /// reflection folder together with the choices of its panel.
    /// </summary>
    /// <remarks>
    /// Like "Create anew", a new draft replaces an earlier one, while a confirmed version stays until the person
    /// confirms the new draft. Since a draft may hold edits of the person, the component asks first when there is one.
    /// </remarks>
    private async Task CreateAllAsync()
    {
        var kinds = this.CreatableKinds;
        if (this.ProviderIssue is not null || kinds.Count is 0)
            return;

        if (kinds.Any(kind => this.results[kind].Draft is not null))
        {
            var parameters = new DialogParameters<ConfirmDialog>
            {
                { dialog => dialog.Message, TA("Replace the drafts you have not confirmed yet? Materials you confirmed stay until you confirm their new drafts.") }
            };

            var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Create all materials in parallel"), parameters, DialogOptions.FULLSCREEN);
            var answer = await reference.Result;
            if (answer is null || answer.Canceled)
                return;
        }

        await Task.WhenAll(kinds.Select(this.CreateAsync));
    }

    private Task CancelAsync(BflResultKind kind) => this.Generator.CancelAsync(new BflResultLocation(this.ProjectId, kind));

    private void StartEditing(BflResultKind kind) => this.editing.Add(kind);

    private void ShowPreview(BflResultKind kind) => this.editing.Remove(kind);

    private async Task SetDraftAsync(BflResultKind kind, string? text)
    {
        if (this.results.GetValueOrDefault(kind) is not { Draft: { } draft } result)
            return;

        draft.Text = text ?? string.Empty;
        await this.SaveAsync(kind, result);
    }

    private async Task ConfirmAsync(BflResultKind kind)
    {
        if (this.results.GetValueOrDefault(kind) is not { Draft: { } draft } result)
            return;

        result.Confirm(draft.Text, DateTimeOffset.UtcNow);
        this.editing.Remove(kind);
        await this.SaveAsync(kind, result);
    }

    private async Task DiscardDraftAsync(BflResultKind kind)
    {
        if (this.results.GetValueOrDefault(kind) is not { Draft: not null } result)
            return;

        result.Draft = null;
        this.editing.Remove(kind);
        await this.SaveAsync(kind, result);
    }

    private async Task EditAsync(BflResultKind kind)
    {
        if (this.results.GetValueOrDefault(kind) is not { Confirmed: not null } result)
            return;

        result.Edit();
        this.editing.Add(kind);
        await this.SaveAsync(kind, result);
    }

    /// <summary>
    /// Exports the confirmed version of a result, with the notice that AI generated it when the person or the
    /// organization switched that on.
    /// </summary>
    /// <remarks>
    /// The format knows who writes it, as in the chat: Pandoc writes documents and HTML, AI Studio itself writes
    /// Markdown.
    /// </remarks>
    /// <param name="kind">The kind of result.</param>
    /// <param name="confirmed">The confirmed version.</param>
    /// <param name="format">The format of the file.</param>
    private async Task ExportAsync(BflResultKind kind, BflResultVersion confirmed, FileExportFormat format)
    {
        var fileName = $"{this.ProjectName} - {kind.Title}";
        this.saving.Add(kind);
        try
        {
            if (format.UsesPandoc)
                await PandocExport.ToDocument(this.RustService, this.PandocAvailability, this.DisclosureService, kind.Title, format, new ContentText { Text = confirmed.Text }, fileName);
            else
                await PlainFileExport.ToFile(this.RustService, kind.Title, format, this.DisclosureService.AddDisclosureToMarkdown(confirmed.Text), fileName);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            this.Logger.LogError(exception, "Could not export a result of the BeyondFacts Lab, because no exporter writes the format {ExportFormat}.", format);
            await this.MessageBus.SendError(new(AppIcons.ERROR, TA("AI Studio could not export this material. Please try another format.")));
        }
        finally
        {
            this.saving.Remove(kind);
        }
    }

    private async Task SaveAsync(BflResultKind kind, BflResult result)
    {
        this.saving.Add(kind);
        try
        {
            var project = await this.Store.SaveResultAsync(this.ProjectId, kind, result);
            await this.ResultStored.InvokeAsync(project);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not save the result '{Kind}' of a preparation in the BeyondFacts Lab.", kind);
            await this.MessageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save this material. Please try again.")));
        }
        finally
        {
            this.saving.Remove(kind);
        }
    }
}