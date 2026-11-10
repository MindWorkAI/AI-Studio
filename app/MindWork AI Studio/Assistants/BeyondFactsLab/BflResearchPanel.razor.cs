using System.Globalization;

using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The area "Research" of the step "Strategy" of the BeyondFacts Lab: it starts the research agents, shows what they
/// found, and shows what left AI Studio for it.
/// </summary>
/// <remarks>
/// The agents work in BflResearchRunner, so that the research goes on while the person looks elsewhere; the area reads
/// the research again whenever the runner says one changed. Above all, the area tells the person which details the
/// research receives, and that nothing else of the preparation reaches a search. For an event which is not public, it
/// shows neither the research nor a way to start it, see BflEventVisibility.
/// </remarks>
public partial class BflResearchPanel : MSGComponentBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    [Inject]
    private BflResearchRunner Runner { get; init; } = null!;

    /// <summary>
    /// Gets or sets the ID of the preparation whose research the area shows.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the forms of the preparation, by their steps, whose public details the research receives.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflStepFormData> Forms { get; set; } = new Dictionary<BflStep, BflStepFormData>();

    /// <summary>
    /// Gets or sets the callback raised after a research was stored, with the preparation and its new time of change.
    /// </summary>
    [Parameter]
    public EventCallback<BflProject> ResearchStored { get; set; }

    /// <summary>
    /// Gets or sets the provider of the preparation, which runs the research.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider cannot run the research, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// The research this version can open, by its topics; a topic without research is missing.
    /// </summary>
    private Dictionary<BflResearchTopic, BflResearch> research = [];

    /// <summary>
    /// Whether the research can search the web with the provider, or null while nobody checked it.
    /// </summary>
    private BflResearchAvailability? availability;

    /// <summary>
    /// The preparation whose research was loaded, to load again when the page shows another one.
    /// </summary>
    private Guid loadedProjectId;

    /// <summary>
    /// The topic whose panel is open, or null while all are closed. Only one is open at a time.
    /// </summary>
    private BflResearchTopic? expandedTopic = BflResearchTopic.All[0];

    /// <summary>
    /// Gets the public details about the event as they are now: what the research receives.
    /// </summary>
    private IReadOnlyList<BflResearchDetail> PublicDetails => BflPromptComposer.PublicDetailsOf(this.Forms);

    /// <summary>
    /// Gets whether the person said that the event is not public, so that the lab never researches it.
    /// </summary>
    private bool IsNotPublic => BflEventVisibility.IsNotPublic(this.Forms);

    /// <summary>
    /// Gets whether the person can start a research right now.
    /// </summary>
    private bool CanStart => !this.IsNotPublic && this.ProviderIssue is null && this.availability is { CanResearch: true } && this.PublicDetails.Count > 0;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.Runner.ResearchChanged += this.OnResearchChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        // The person may change the tool settings or the provider meanwhile, so the area checks anew every time:
        this.availability = await this.Runner.CheckAvailabilityAsync(this.Provider);
        if (this.loadedProjectId == this.ProjectId)
            return;

        this.loadedProjectId = this.ProjectId;
        this.research = [];
        await this.LoadAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.Runner.ResearchChanged -= this.OnResearchChangedAsync;
        base.DisposeResources();
    }

    private bool IsRunning(BflResearchTopic topic) => this.Runner.IsRunning(new BflResearchLocation(this.ProjectId, topic));

    private void SetExpanded(BflResearchTopic topic, bool expanded)
    {
        if (expanded)
            this.expandedTopic = topic;
        else if (this.expandedTopic == topic)
            this.expandedTopic = null;
    }

    private async Task StartAllAsync()
    {
        if (!this.CanStart)
            return;

        await this.Runner.RunAllAsync(this.ProjectId, this.Provider);
    }

    private async Task StartAsync(BflResearchTopic topic)
    {
        if (!this.CanStart)
            return;

        this.expandedTopic = topic;
        await this.Runner.RunAsync(new BflResearchLocation(this.ProjectId, topic), this.Provider);
    }

    private Task CancelAsync(BflResearchTopic topic) => this.Runner.CancelAsync(new BflResearchLocation(this.ProjectId, topic));

    private string StatusIcon(BflResearch? found) => found switch
    {
        null => Icons.Material.Outlined.Circle,
        _ when found.IsOutdated(this.PublicDetails) => Icons.Material.Filled.Update,
        _ => Icons.Material.Filled.CheckCircle,
    };

    private Color StatusColor(BflResearch? found) => found switch
    {
        null => Color.Default,
        _ when found.IsOutdated(this.PublicDetails) => Color.Warning,
        _ => Color.Success,
    };

    private string StatusText(BflResearch? found, bool isRunning)
    {
        if (isRunning)
            return T("Researching");

        if (found is null)
            return T("Not researched yet");

        return found.IsOutdated(this.PublicDetails)
            ? T("Outdated")
            : string.Format(Culture, T("Researched on {0}"), found.CreatedAtUtc.ToLocalTime().ToString("g", Culture));
    }

    /// <summary>
    /// Gets the label of the field of a public detail.
    /// </summary>
    /// <param name="detail">The public detail.</param>
    private static string LabelOf(BflResearchDetail detail) => BflFieldCatalog.FIELDS.FirstOrDefault(field => string.Equals(field.Id, detail.FieldId, StringComparison.Ordinal))?.Label ?? detail.FieldId;

    /// <summary>
    /// Reads the research of the preparation again once an agent started, ended, or stored its research.
    /// </summary>
    /// <param name="location">The research which changed.</param>
    private Task OnResearchChangedAsync(BflResearchLocation location)
    {
        if (location.ProjectId != this.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            await this.LoadAsync();
            if (!this.Runner.IsRunning(location) && await this.Store.LoadAsync(location.ProjectId) is { } project)
                await this.ResearchStored.InvokeAsync(project);

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Reads the research of the preparation shown.
    /// </summary>
    private async Task LoadAsync()
    {
        var projectId = this.ProjectId;
        var loaded = await this.Store.LoadAllResearchAsync(projectId);

        // The page may have shown another preparation meanwhile:
        if (projectId == this.ProjectId)
            this.research = new(loaded);
    }
}