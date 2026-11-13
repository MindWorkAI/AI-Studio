using System.Text.Json.Serialization;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// A task of a comparison series: the user works on it with one model in the first chat, then with the other model in
/// the second chat, and decides which chat helped more.
/// </summary>
/// <remarks>
/// The vote and the choice of the judge are kept in terms of the models of the series, FIRST for its first model, not
/// in terms of the chats: which model the first chat had is chosen at random for every task, so that the order in which
/// the user meets them does not favor one model.
/// </remarks>
public sealed record ModelComparisonSeriesTask
{
    /// <summary>
    /// The ID of the task.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// When the user started the task, in UTC.
    /// </summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>
    /// When the user decided, in UTC, or null before.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>
    /// The beginning of the first message, so that the user recognizes the task again.
    /// </summary>
    public string QuestionPreview { get; set; } = string.Empty;

    /// <summary>
    /// Whether the first model of the series answers in the first chat; otherwise, the second model does.
    /// </summary>
    public bool FirstModelIsChatOne { get; set; }

    /// <summary>
    /// The chat the user works in first.
    /// </summary>
    public ModelComparisonSeriesChat ChatOne { get; set; } = new();

    /// <summary>
    /// The chat the user works in second, which starts with the same first message.
    /// </summary>
    public ModelComparisonSeriesChat ChatTwo { get; set; } = new();

    /// <summary>
    /// How far the task got.
    /// </summary>
    public ModelComparisonSeriesTaskState State { get; set; } = ModelComparisonSeriesTaskState.FIRST_CHAT;

    /// <summary>
    /// The model the user chose, in terms of the models of the series, or null before the decision.
    /// </summary>
    public ModelComparisonModelChoice? Vote { get; set; }

    /// <summary>
    /// The model the judge chose, in terms of the models of the series, or null without a usable verdict.
    /// </summary>
    public ModelComparisonModelChoice? JudgeChoice { get; set; }

    /// <summary>
    /// Why the judge chose so, or empty without a usable verdict.
    /// </summary>
    public string JudgeReasoning { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user decided without knowing the models, i.e., before the result of the series was shown.
    /// </summary>
    public bool IsBlind { get; set; } = true;

    /// <summary>
    /// The chat in which the first model of the series answers.
    /// </summary>
    [JsonIgnore]
    public ModelComparisonSeriesChat ChatOfFirstModel => this.FirstModelIsChatOne ? this.ChatOne : this.ChatTwo;

    /// <summary>
    /// The chat in which the second model of the series answers.
    /// </summary>
    [JsonIgnore]
    public ModelComparisonSeriesChat ChatOfSecondModel => this.FirstModelIsChatOne ? this.ChatTwo : this.ChatOne;
}