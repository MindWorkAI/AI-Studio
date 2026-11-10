using System.Globalization;

using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

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
    /// Exports the confirmed version of a result as a document, with the notice that AI generated it when the person
    /// or the organization switched that on.
    /// </summary>
    /// <param name="kind">The kind of result.</param>
    /// <param name="confirmed">The confirmed version.</param>
    /// <param name="format">The format of the document.</param>
    private async Task ExportAsync(BflResultKind kind, BflResultVersion confirmed, FileExportFormat format)
    {
        this.saving.Add(kind);
        try
        {
            await PandocExport.ToDocument(this.RustService, this.PandocAvailability, this.DisclosureService, kind.Title, format, new ContentText { Text = confirmed.Text }, $"{this.ProjectName} - {kind.Title}");
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
            await this.MessageBus.SendError(new(Icons.Material.Filled.SaveAs, TA("AI Studio could not save this material. Please try again.")));
        }
        finally
        {
            this.saving.Remove(kind);
        }
    }
}