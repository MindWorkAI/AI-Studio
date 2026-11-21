using System.Globalization;

using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Lets the person choose whom the conversation of the step "Test situation" plays: the mode, and the people from the
/// research on single participants.
/// </summary>
/// <remarks>
/// The choice is part of the details of the step, see BflStepFormData.Simulation; the page stores it like every other
/// detail. Only the people who qualify can be chosen, see BflSimulationCandidates, and only as many as the mode allows.
/// The area tells the person how much of the context window of the model the research on the chosen people takes,
/// and refuses, like the conversation, when a known window is likely too small.
/// </remarks>
public partial class BflSimulationChoice : MSGComponentBase
{
    [Inject]
    private BeyondFactsLabStore Store { get; init; } = null!;

    /// <summary>
    /// Gets or sets the ID of the preparation.
    /// </summary>
    [Parameter, EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the forms of the preparation, by their steps, which tell whether the event is public.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyDictionary<BflStep, BflStepFormData> Forms { get; set; } = new Dictionary<BflStep, BflStepFormData>();

    /// <summary>
    /// Gets or sets the form of the step "Test situation", which keeps the choice.
    /// </summary>
    [Parameter, EditorRequired]
    public BflStepFormData Form { get; set; } = new();

    /// <summary>
    /// Gets or sets what happens after the person changed the choice, e.g., storing the form.
    /// </summary>
    [Parameter]
    public EventCallback FormChanged { get; set; }

    /// <summary>
    /// Gets or sets the provider of the preparation, whose model decides how much research fits.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets whether the person cannot change the choice right now.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets further CSS classes of the area.
    /// </summary>
    [Parameter]
    public string Class { get; set; } = string.Empty;

    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// The research on the topics this version can open, with the list of participants.
    /// </summary>
    private IReadOnlyDictionary<BflResearchTopic, BflResearch> topics = new Dictionary<BflResearchTopic, BflResearch>();

    /// <summary>
    /// The research on every person stored for the preparation.
    /// </summary>
    private IReadOnlyList<BflPersonResearch> people = [];

    /// <summary>
    /// The preparation whose research was loaded, to load again when the page shows another one.
    /// </summary>
    private Guid loadedProjectId;

    private BflSimulationMode Mode => this.Form.Simulation?.Mode ?? BflSimulationMode.GENERIC;

    private IReadOnlyList<BflSimulatedPerson> Candidates => BflSimulationCandidates.Of(this.Forms, this.topics, this.people);

    private BflEffectiveSimulation Effective => BflEffectiveSimulation.Of(this.Form.Simulation, this.Candidates);

    private IReadOnlyCollection<string> ChosenNames => [.. this.Effective.People.Select(person => person.Name)];

    private string ChooseText => this.Mode.MaxPeople is 1 ? TA("Choose the person:") : string.Format(Culture, TA("Choose up to {0} people:"), this.Mode.MaxPeople);

    /// <summary>
    /// Gets whether the model of the provider is known: without a provider, the area says nothing about the window.
    /// </summary>
    private bool HasProvider => this.Provider.UsedLLMProvider is not LLMProviders.NONE;

    private BflSimulationFit Fit => this.HasProvider ? BflSimulationBudget.Check(this.Effective, this.Provider.GetModelProfile().Context) : BflSimulationFit.FITS;

    private int DossierTokens => BflSimulationBudget.DossierTokens(this.Effective.People);

    private int ContextTokens => this.HasProvider ? this.Provider.GetModelProfile().Context.DefaultTokens : 0;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.loadedProjectId == this.ProjectId)
            return;

        this.loadedProjectId = this.ProjectId;
        this.topics = new Dictionary<BflResearchTopic, BflResearch>();
        this.people = [];
        await this.LoadAsync();
    }

    /// <summary>
    /// Tells whether a person cannot be chosen, because the mode allows no more people. With room for one person only,
    /// choosing another one replaces the first, see ChooseAsync.
    /// </summary>
    /// <param name="candidate">The person.</param>
    private bool IsOverTheLimit(BflSimulatedPerson candidate)
    {
        var chosen = this.ChosenNames;
        return this.Mode.MaxPeople > 1 && chosen.Count >= this.Mode.MaxPeople && !chosen.Contains(candidate.Name);
    }

    private async Task SetModeAsync(BflSimulationMode mode)
    {
        if (this.Form.Simulation is null && mode is BflSimulationMode.GENERIC)
            return;

        this.Form.Simulation ??= new();
        this.Form.Simulation.Mode = mode;
        await this.FormChanged.InvokeAsync();
    }

    private async Task ChooseAsync(IReadOnlyCollection<string>? names)
    {
        var chosen = names ?? [];
        var current = this.ChosenNames;

        // The person chose these just now; with room for one person only, the newest one replaces the others:
        List<string> kept = [.. current.Where(chosen.Contains), .. chosen.Where(name => !current.Contains(name))];
        if (kept.Count > this.Mode.MaxPeople)
            kept = [.. kept.TakeLast(this.Mode.MaxPeople)];

        this.Form.Simulation ??= new() { Mode = this.Mode };
        this.Form.Simulation.People = kept;
        await this.FormChanged.InvokeAsync();
    }

    private async Task LoadAsync()
    {
        var projectId = this.ProjectId;
        var loadedTopics = await this.Store.LoadAllResearchAsync(projectId);
        var loadedPeople = await this.Store.LoadAllPersonResearchAsync(projectId);

        // The page may have shown another preparation meanwhile:
        if (projectId != this.ProjectId)
            return;

        this.topics = loadedTopics;
        this.people = loadedPeople;
    }
}