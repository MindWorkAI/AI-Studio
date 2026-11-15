using System.Globalization;

using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The part of the area "Research" in which the person chooses whom of the list of participants the BeyondFacts Lab
/// researches in depth, starts the research on these people, and sees what each stage found and what left AI Studio
/// for it.
/// </summary>
/// <remarks>
/// The agents work in BflResearchRunner, so that the research goes on while the person looks elsewhere; the part
/// reads the research and the selection again whenever the runner says one of them changed. Nothing starts by itself:
/// the research on people costs up to three agents for each person, so the person starts it.
/// </remarks>
public partial class BflPersonResearchPanel : MSGComponentBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private BflResearchRunner Runner { get; init; } = null!;

    [Inject]
    private ILogger<BflPersonResearchPanel> Logger { get; init; } = null!;

    /// <summary>
    /// Gets or sets the ID of the preparation.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the research on the participants, with its list of people, or null when there is none yet.
    /// </summary>
    [Parameter]
    public BflResearch? List { get; set; }

    /// <summary>
    /// Gets or sets the public details about the event as they are now, to tell an outdated research.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BflResearchDetail> PublicDetails { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a research may start: the event is public, the provider fits, and the web search works.
    /// </summary>
    [Parameter]
    public bool CanStart { get; set; }

    /// <summary>
    /// Gets or sets the provider of the preparation.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider of the preparation does not fit, or null when it does.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    /// <summary>
    /// Gets or sets the callback raised after a research on a person ended, with the preparation and its new time of
    /// change.
    /// </summary>
    [Parameter]
    public EventCallback<BflProject> ResearchStored { get; set; }

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    private Dictionary<Guid, BflPersonResearch> dossiers = [];

    private BflParticipantSelection? selection;

    private Guid loadedProjectId;

    private Guid? expandedPerson;

    private bool IsSuggesting => this.Runner.IsSuggesting(this.ProjectId);

    private bool IsSuggestion => this.List is not null && this.selection is { Source: BflParticipantSelectionSource.SUGGESTION } suggested && suggested.IsFor(this.List);

    private IReadOnlyList<BflParticipant> Selected => this.List is null ? [] : BflParticipantSelection.SelectedIn(this.List, this.selection);

    private IReadOnlyCollection<string> SelectedNames => [.. this.Selected.Select(participant => participant.Name)];

    /// <summary>
    /// The people with an area of their own: the chosen ones, and those with a research or a running one, in the order
    /// of the list.
    /// </summary>
    private IReadOnlyList<BflParticipant> Shown
    {
        get
        {
            var selected = this.Selected;
            return [.. (this.List?.Participants ?? []).Where(participant => selected.Contains(participant) || this.dossiers.ContainsKey(BflPersonId.Of(participant.Name)) || this.Runner.IsRunning(this.LocationOf(participant)))];
        }
    }

    /// <summary>
    /// The chosen people whom "Research the chosen people" starts: those without a complete, current research, which
    /// do not run already. Researching a person again is a step of its own, since it costs agents again.
    /// </summary>
    private IReadOnlyList<BflParticipant> ToStart => [.. this.Selected.Where(participant =>
        !this.Runner.IsRunning(this.LocationOf(participant))
        && (this.dossiers.GetValueOrDefault(BflPersonId.Of(participant.Name)) is not { IsComplete: true } found || found.IsOutdated(this.PublicDetails)))];

    protected override async Task OnInitializedAsync()
    {
        this.Runner.PersonResearchChanged += this.OnPersonResearchChangedAsync;
        this.Runner.SelectionChanged += this.OnSelectionChangedAsync;
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.loadedProjectId == this.ProjectId)
            return;

        this.loadedProjectId = this.ProjectId;
        this.dossiers = [];
        this.selection = null;
        this.expandedPerson = null;
        await this.LoadDossiersAsync();
        await this.LoadSelectionAsync();
    }

    protected override void DisposeResources()
    {
        this.Runner.PersonResearchChanged -= this.OnPersonResearchChangedAsync;
        this.Runner.SelectionChanged -= this.OnSelectionChangedAsync;
        base.DisposeResources();
    }

    private BflPersonLocation LocationOf(BflParticipant participant) => BflPersonLocation.Of(this.ProjectId, participant.Name);

    private void SetExpanded(Guid personId, bool expanded)
    {
        if (expanded)
            this.expandedPerson = personId;
        else if (this.expandedPerson == personId)
            this.expandedPerson = null;
    }

    private async Task ChooseAsync(IReadOnlyCollection<string>? names)
    {
        if (this.List is null)
            return;

        var chosen = new BflParticipantSelection
        {
            Names = [.. this.List.Participants.Where(participant => names?.Contains(participant.Name) is true).Select(participant => participant.Name)],
            Source = BflParticipantSelectionSource.PERSON,
            ListCreatedAtUtc = this.List.CreatedAtUtc,
        };

        try
        {
            var project = await this.Store.SaveParticipantSelectionAsync(this.ProjectId, chosen);
            this.selection = chosen;
            await this.ResearchStored.InvokeAsync(project);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not store the selection of participants of a preparation of the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not store the selection. Please try again.")));
        }
    }

    private async Task StartChosenAsync()
    {
        if (!this.CanStart)
            return;

        var people = this.ToStart;
        if (people.Count is 0)
            return;

        this.expandedPerson = BflPersonId.Of(people[0].Name);
        await this.Runner.RunPeopleAsync(this.ProjectId, people.Select(participant => participant.Name), this.Provider);
    }

    private async Task StartPersonAsync(BflParticipant participant)
    {
        if (!this.CanStart)
            return;

        this.expandedPerson = BflPersonId.Of(participant.Name);
        await this.Runner.RunPersonAsync(this.ProjectId, participant.Name, this.Provider);
    }

    private Task CancelAsync(BflPersonLocation location) => this.Runner.CancelAsync(location);

    private Task SuggestAsync() => this.Runner.SuggestSelectionAsync(this.ProjectId, this.Provider);

    private string StatusIcon(BflPersonResearch? found) => found switch
    {
        null => AppIcons.NOT_STARTED,
        _ when found.IsOutdated(this.PublicDetails) => AppIcons.OUTDATED,
        { IsComplete: false } => AppIcons.RESEARCH_INCOMPLETE,
        _ => AppIcons.SUCCESS,
    };

    private Color StatusColor(BflPersonResearch? found) => found switch
    {
        null => Color.Default,
        _ when found.IsOutdated(this.PublicDetails) => Color.Warning,
        { IsComplete: false } => Color.Warning,
        _ => Color.Success,
    };

    private string StatusText(BflPersonLocation location, BflPersonResearch? found)
    {
        if (this.Runner.IsRunning(location))
        {
            return this.Runner.RunningStageOf(location) is { } stage
                ? string.Format(Culture, T("Researching: {0} ({1} of {2})"), stage.Title, BflPersonResearchStage.All.ToList().IndexOf(stage) + 1, BflPersonResearchStage.All.Count)
                : T("Waiting for a free agent");
        }

        if (found is null)
            return T("Not researched yet");

        if (found.IsOutdated(this.PublicDetails))
            return T("Outdated");

        if (!found.IsComplete)
            return T("Incomplete");

        return found.LastCreatedAtUtc is { } createdAtUtc
            ? string.Format(Culture, T("Researched on {0}"), createdAtUtc.ToLocalTime().ToString("g", Culture))
            : T("Not researched yet");
    }

    private Task OnPersonResearchChangedAsync(BflPersonLocation location)
    {
        if (location.ProjectId != this.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            await this.LoadDossiersAsync();
            if (!this.Runner.IsRunning(location) && await this.Store.LoadAsync(location.ProjectId) is { } project)
                await this.ResearchStored.InvokeAsync(project);

            this.StateHasChanged();
        });
    }

    private Task OnSelectionChangedAsync(Guid projectId)
    {
        if (projectId != this.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            await this.LoadSelectionAsync();
            this.StateHasChanged();
        });
    }

    private async Task LoadDossiersAsync()
    {
        var projectId = this.ProjectId;
        var loaded = await this.Store.LoadAllPersonResearchAsync(projectId);

        // The page may have shown another preparation meanwhile:
        if (projectId == this.ProjectId)
            this.dossiers = loaded.ToDictionary(research => BflPersonId.Of(research.Name));
    }

    private async Task LoadSelectionAsync()
    {
        var projectId = this.ProjectId;
        BflParticipantSelection? loaded;
        try
        {
            loaded = await this.Store.LoadParticipantSelectionAsync(projectId);
        }
        catch (InvalidDataException exception)
        {
            this.Logger.LogWarning(exception, "Could not open the selection of participants of a preparation of the BeyondFacts Lab.");
            loaded = null;
        }

        if (projectId == this.ProjectId)
            this.selection = loaded;
    }
}