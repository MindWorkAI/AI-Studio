using System.Text.Json.Serialization;

using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// A comparison series: two models compared on real tasks of the user, over days or weeks, with the same profile,
/// tools, and data sources for both.
/// </summary>
/// <remarks>
/// The series keeps the providers by their IDs, to ask them, and the identities of their models as they were when the
/// series began, so that a provider whose model changed meanwhile is noticed rather than compared under a wrong name.
/// Its chats lie in the folder of the series, see ModelComparisonStore, and stay readable there.
/// </remarks>
public sealed record ModelComparisonSeries
{
    /// <summary>
    /// The ID of the series, which names its folder.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name the user gave the series.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the user began the series, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// The ID of the provider of the first model.
    /// </summary>
    public string FirstProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the provider of the second model.
    /// </summary>
    public string SecondProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The first model, as it was when the series began.
    /// </summary>
    public ModelComparisonModelIdentity First { get; set; } = new();

    /// <summary>
    /// The second model, as it was when the series began.
    /// </summary>
    public ModelComparisonModelIdentity Second { get; set; } = new();

    /// <summary>
    /// The ID of the provider of the judge, or empty without a judge.
    /// </summary>
    public string JudgeProviderId { get; set; } = string.Empty;

    /// <summary>
    /// The judge, as it was when the series began, or null without a judge.
    /// </summary>
    public ModelComparisonModelIdentity? Judge { get; set; }

    /// <summary>
    /// The expectations, values, and goals of the user, by which the judge decides, see ModelComparisonJudgeExpectations;
    /// empty when the user confirmed that the judge may decide by its own standards. Fixed with the series like its
    /// models, so that the judge decides every task by the same standards.
    /// </summary>
    public string JudgeInstructions { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the profile both chats use.
    /// </summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// The tools both chats may use.
    /// </summary>
    public List<string> ToolIds { get; set; } = [];

    /// <summary>
    /// The IDs of the data sources both chats may search.
    /// </summary>
    public List<string> DataSourceIds { get; set; } = [];

    /// <summary>
    /// How both chats search the data sources, or null without data sources. The same for both, so that neither model
    /// gets passages which the other one has to look for itself.
    /// </summary>
    public DataSourceRetrievalMode? RetrievalMode { get; set; }

    /// <summary>
    /// Whether the user saw the result of the series, which names the models. Tasks decided afterward are no longer
    /// blind.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// When the user saw the result of the series first, in UTC, or null before.
    /// </summary>
    public DateTimeOffset? ResolvedAtUtc { get; set; }

    /// <summary>
    /// The tasks of the series, in the order they were started.
    /// </summary>
    public List<ModelComparisonSeriesTask> Tasks { get; set; } = [];

    /// <summary>
    /// When the user last worked on the series, in UTC.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset LastActivityUtc => this.Tasks
        .Select(task => task.FinishedAtUtc ?? task.StartedAtUtc)
        .Append(this.CreatedAtUtc)
        .Max();

    /// <summary>
    /// The task the user works on right now, or null when every task is decided.
    /// </summary>
    [JsonIgnore]
    public ModelComparisonSeriesTask? OpenTask => this.Tasks.LastOrDefault(task => task.State is not ModelComparisonSeriesTaskState.DONE);
}