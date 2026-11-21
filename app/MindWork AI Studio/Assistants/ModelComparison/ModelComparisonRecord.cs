using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the Model Comparison assistant keeps of a batch, so that the statistics can tell later which model came out
/// ahead across many comparisons.
/// </summary>
/// <remarks>
/// Only the results are kept: which models took part, the votes, the choices of the judge, and the times. Neither the
/// answers, nor the context, nor the reasoning or the instructions of the judge are kept; the question only in short,
/// so that the user recognizes a comparison again. A stored comparison names its file by the ID of its batch, so
/// storing the same batch again, after the session was restored for example, replaces the file instead of counting
/// the batch twice.
/// </remarks>
public sealed record ModelComparisonRecord
{
    /// <summary>
    /// The ID of the batch, which names the file.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// When the user was through with the batch, in UTC.
    /// </summary>
    public DateTimeOffset FinishedAtUtc { get; set; }

    /// <summary>
    /// The beginning of the question, as the summary of the batch showed it.
    /// </summary>
    public string QuestionPreview { get; set; } = string.Empty;

    /// <summary>
    /// The first model.
    /// </summary>
    public ModelComparisonModelIdentity First { get; set; } = new();

    /// <summary>
    /// The second model.
    /// </summary>
    public ModelComparisonModelIdentity Second { get; set; } = new();

    /// <summary>
    /// The judge, or null when the batch had none.
    /// </summary>
    public ModelComparisonModelIdentity? Judge { get; set; }

    /// <summary>
    /// The runs which arrived, in the order they arrived.
    /// </summary>
    public List<ModelComparisonRecordRun> Runs { get; set; } = [];

    /// <summary>
    /// Whether any run holds a vote or a choice of the judge; only then the batch is worth keeping.
    /// </summary>
    [JsonIgnore]
    public bool HoldsAnyChoice => this.Runs.Any(run => run.Vote is not null || run.JudgeChoice is not null);

    /// <summary>
    /// Gets what the assistant keeps of a batch.
    /// </summary>
    /// <param name="batch">The batch, which the user is through with.</param>
    /// <param name="finishedAtUtc">When the user was through with it.</param>
    /// <param name="questionPreview">The beginning of the question.</param>
    /// <param name="first">The first model.</param>
    /// <param name="second">The second model.</param>
    /// <param name="judge">The judge, or null without one.</param>
    /// <returns>The stored comparison.</returns>
    public static ModelComparisonRecord Of(ModelComparisonBatch batch, DateTimeOffset finishedAtUtc, string questionPreview, ModelComparisonModelIdentity first, ModelComparisonModelIdentity second, ModelComparisonModelIdentity? judge) => new()
    {
        Id = batch.Id,
        FinishedAtUtc = finishedAtUtc.ToUniversalTime(),
        QuestionPreview = questionPreview,
        First = first,
        Second = second,
        Judge = judge,
        Runs =
        [
            .. batch.Entries.Select((entry, index) => new ModelComparisonRecordRun
            {
                Vote = batch.VoteChoiceAt(index),
                JudgeChoice = batch.JudgeChoiceAt(index),
                First = ModelComparisonRecordAnswer.Of(entry.Result.First),
                Second = ModelComparisonRecordAnswer.Of(entry.Result.Second),
            }),
        ],
    };
}