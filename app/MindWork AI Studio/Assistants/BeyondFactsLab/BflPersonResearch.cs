namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What the agents of the BeyondFacts Lab found on the web about one person from the list of participants, stored as
/// research/participants/&lt;id&gt;.json in the folder of the step "Strategy", see BflPersonId.
/// </summary>
/// <remarks>
/// <para>
/// The research on a person runs in stages, see BflPersonResearchStage. Each stage keeps its text together with what
/// left AI Studio for it, like the research on a topic, so that the person sees every query and every page.
/// </para>
/// <para>
/// A new research replaces the earlier one as soon as its first stage is done: then the research holds the stages of
/// the new run only, until it is complete. A research stopped before its first stage leaves the earlier one as it was.
/// </para>
/// </remarks>
public sealed class BflPersonResearch
{
    /// <summary>
    /// The version of the stored form this version of AI Studio writes. Raise it with every new property.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    /// <summary>
    /// How many characters of the answer of the agent a stage keeps at most. The research on several people goes into
    /// the prompt of the step "Strategy", so each stage stays well below the limit of a topic.
    /// </summary>
    public const int MAX_STAGE_TEXT_CHARACTERS = 8_000;

    /// <summary>
    /// Gets or sets the version of the stored form.
    /// </summary>
    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>
    /// Gets or sets the name of the person, as the list of participants gives it.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the stages found, in the order in which they ran.
    /// </summary>
    public List<BflPersonStageResearch> Stages { get; set; } = [];

    /// <summary>
    /// Tells whether every stage is done.
    /// </summary>
    public bool IsComplete => BflPersonResearchStage.All.All(stage => this.StageOf(stage) is not null);

    /// <summary>
    /// Gets when the last stage was done, or null when none is.
    /// </summary>
    public DateTimeOffset? LastCreatedAtUtc => this.Stages.Count is 0 ? null : this.Stages.Max(stage => stage.Research.CreatedAtUtc);

    /// <summary>
    /// Gets what a stage found.
    /// </summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The research of the stage, or null when the stage is not done.</returns>
    public BflResearch? StageOf(BflPersonResearchStage stage) => this.Stages.FirstOrDefault(candidate => candidate.Stage == stage)?.Research;

    /// <summary>
    /// Keeps what a stage found, in place of what it found before.
    /// </summary>
    /// <param name="stage">The stage.</param>
    /// <param name="research">What the stage found.</param>
    public void Set(BflPersonResearchStage stage, BflResearch research)
    {
        this.Stages.RemoveAll(candidate => candidate.Stage == stage);
        this.Stages.Add(new BflPersonStageResearch { Stage = stage, Research = research });
        this.Stages.Sort((first, second) => first.Stage.CompareTo(second.Stage));
    }

    /// <summary>
    /// Tells whether the public details about the event changed since one of the stages received them.
    /// </summary>
    /// <param name="details">The public details as they are now, see BflPromptComposer.PublicDetailsOf.</param>
    public bool IsOutdated(IReadOnlyList<BflResearchDetail> details) => this.Stages.Any(stage => stage.Research.IsOutdated(details));
}