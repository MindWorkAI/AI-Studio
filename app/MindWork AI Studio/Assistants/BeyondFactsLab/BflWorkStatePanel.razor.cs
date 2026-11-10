using System.Globalization;

using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The work state of one step of the BeyondFacts Lab: the draft the lab writes from the conversation, the work state
/// the person confirmed, and the suggestions for the empty fields of the form.
/// </summary>
/// <remarks>
/// The component changes the work state it gets and stores it; the page keeps the work states of all steps, since the
/// status of a step depends on the earlier ones. The model writes in BflWorkStateGenerator, so that the work goes on
/// while the person looks elsewhere; the page renders whenever the generator says a work state changed.
/// </remarks>
public partial class BflWorkStatePanel : MSGComponentBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private BflWorkStateGenerator Generator { get; init; } = null!;

    [Inject]
    private ILogger<BflWorkStatePanel> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the step whose work state the component shows.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStepThreadLocation Location { get; set; } = null!;

    /// <summary>
    /// Gets or sets the work states of the preparation, by their steps; a step which is missing has a work state this
    /// version cannot open.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflWorkState> WorkStates { get; set; } = new Dictionary<BflStep, BflWorkState>();

    /// <summary>
    /// Gets or sets the forms of the preparation, by their steps; a step which is missing has a form this version
    /// cannot open.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflStepFormData> Forms { get; set; } = new Dictionary<BflStep, BflStepFormData>();

    /// <summary>
    /// Gets or sets the callback raised after the person took over a suggestion into the form of the step, which the
    /// parent then stores.
    /// </summary>
    [Parameter]
    public EventCallback FormChanged { get; set; }

    /// <summary>
    /// Gets or sets the callback raised after the work state was stored, with the preparation and its new time of
    /// change.
    /// </summary>
    [Parameter]
    public EventCallback<BflProject> WorkStateStored { get; set; }

    /// <summary>
    /// Gets or sets the callback raised after the person confirmed the work state and it was stored, with its step,
    /// e.g., to start the research once the event details are confirmed.
    /// </summary>
    [Parameter]
    public EventCallback<BflStep> WorkStateConfirmed { get; set; }

    /// <summary>
    /// Gets or sets the provider which writes the work state.
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
    /// Whether the component stores the work state right now.
    /// </summary>
    private bool isSaving;

    private BflStep Step => this.Location.Step;

    private BflWorkState? WorkState => this.WorkStates.GetValueOrDefault(this.Step);

    private BflStepFormData? Form => this.Forms.GetValueOrDefault(this.Step);

    private IReadOnlyList<BflWorkStateSection> Sections => BflWorkStateSections.ForStep(this.Step);

    private BflWorkStateStatus Status => BflStaleness.Of(this.Step, this.WorkStates, this.Forms);

    private bool IsGenerating => this.Generator.IsRunning(this.Location);

    private bool IsBusy => this.isSaving || this.IsGenerating;

    /// <summary>
    /// Gets the suggestions for fields which are still empty, in the order the form shows the fields.
    /// </summary>
    private IReadOnlyList<KeyValuePair<BflFieldDefinition, string>> VisibleSuggestions
    {
        get
        {
            if (this.WorkState is not { } workState || this.Form is not { } form || workState.FieldSuggestions.Count is 0)
                return [];

            return [.. BflMarkdownSections.SuggestableFields(this.Step, form)
                .Where(definition => workState.FieldSuggestions.ContainsKey(definition.Id))
                .Select(definition => new KeyValuePair<BflFieldDefinition, string>(definition, workState.FieldSuggestions[definition.Id]))];
        }
    }

    private Task GenerateAsync() => this.Generator.GenerateAsync(this.Location, this.Provider);

    private Task CancelAsync() => this.Generator.CancelAsync(this.Location);

    private async Task SetDraftAsync(string key, string? value)
    {
        if (this.WorkState is not { Draft: { } draft } workState || string.Equals(draft.GetValueOrDefault(key, string.Empty), value ?? string.Empty, StringComparison.Ordinal))
            return;

        draft[key] = value ?? string.Empty;
        await this.SaveAsync(workState);
    }

    private async Task ConfirmDraftAsync()
    {
        if (this.WorkState is not { Draft: { } draft } workState)
            return;

        this.Confirm(workState, draft);
        if (await this.SaveAsync(workState))
            await this.WorkStateConfirmed.InvokeAsync(this.Step);
    }

    private async Task ConfirmAgainAsync()
    {
        if (this.WorkState is not { Confirmed: { } confirmed } workState)
            return;

        this.Confirm(workState, confirmed.Sections);
        if (await this.SaveAsync(workState))
            await this.WorkStateConfirmed.InvokeAsync(this.Step);
    }

    private void Confirm(BflWorkState workState, IReadOnlyDictionary<string, string> sections)
    {
        // A form this version cannot open has no fingerprint; its changes then do not count, see BflStaleness:
        var fingerprint = this.Form is { } form ? BflStaleness.FingerprintOf(form) : string.Empty;
        workState.Confirm(sections, BflStaleness.UpstreamRevisionsOf(this.Step, this.WorkStates), fingerprint, DateTimeOffset.UtcNow);
    }

    private async Task DiscardDraftAsync()
    {
        if (this.WorkState is not { } workState)
            return;

        workState.Draft = null;
        await this.SaveAsync(workState);
    }

    private async Task EditAsync()
    {
        if (this.WorkState is not { Confirmed: { } confirmed } workState)
            return;

        workState.Draft = this.DraftOf(confirmed.Sections);
        await this.SaveAsync(workState);
    }

    private async Task WithdrawAsync()
    {
        if (this.WorkState is not { Confirmed: { } confirmed } workState)
            return;

        // The text stays as a draft, so that withdrawing loses nothing:
        workState.Draft ??= this.DraftOf(confirmed.Sections);
        workState.Withdraw();
        await this.SaveAsync(workState);
    }

    private async Task TakeOverSuggestionAsync(BflFieldDefinition field, string value)
    {
        if (this.WorkState is not { } workState || this.Form is not { } form)
            return;

        form.Set(field.Id, value.Length > field.Kind.MaxLength ? value[..field.Kind.MaxLength] : value);
        await this.FormChanged.InvokeAsync();

        workState.FieldSuggestions.Remove(field.Id);
        await this.SaveAsync(workState);
    }

    private async Task DismissSuggestionAsync(BflFieldDefinition field)
    {
        if (this.WorkState is not { } workState)
            return;

        workState.FieldSuggestions.Remove(field.Id);
        await this.SaveAsync(workState);
    }

    /// <summary>
    /// Gets a draft with every section of the step, filled with the texts given.
    /// </summary>
    /// <param name="sections">The texts, by the keys of their sections.</param>
    private Dictionary<string, string> DraftOf(IReadOnlyDictionary<string, string> sections) => this.Sections.ToDictionary(section => section.Key, section => sections.GetValueOrDefault(section.Key, string.Empty), StringComparer.Ordinal);

    /// <summary>
    /// Stores the work state and tells the page.
    /// </summary>
    /// <param name="workState">The work state.</param>
    /// <returns>True when it was stored; what went wrong, the person was told.</returns>
    private async Task<bool> SaveAsync(BflWorkState workState)
    {
        this.isSaving = true;
        try
        {
            var project = await this.Store.SaveWorkStateAsync(this.Location.ProjectId, this.Step, workState);
            await this.WorkStateStored.InvokeAsync(project);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not save the work state of a step in the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.SaveAs, TA("AI Studio could not save the work state. Please try again.")));
            return false;
        }
        finally
        {
            this.isSaving = false;
        }
    }
}