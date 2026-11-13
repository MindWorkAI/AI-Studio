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
    /// Why the judge chose so, or empty without a usable verdict. It names the chats as "Chat 1" and "Chat 2", never a
    /// model.
    /// </summary>
    public string JudgeReasoning { get; set; } = string.Empty;

    /// <summary>
    /// What became of the judge for this task.
    /// </summary>
    public ModelComparisonSeriesJudgeStatus JudgeStatus { get; set; } = ModelComparisonSeriesJudgeStatus.NOT_ASKED;

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

    /// <summary>
    /// Which model of the series answers in the first chat, which the decision shows on the left.
    /// </summary>
    [JsonIgnore]
    public ModelComparisonPresentationOrder Order => this.FirstModelIsChatOne
        ? ModelComparisonPresentationOrder.FIRST_MODEL_FIRST
        : ModelComparisonPresentationOrder.SECOND_MODEL_FIRST;

    /// <summary>
    /// The chat the user works in right now, or null while the user decides or after the decision.
    /// </summary>
    [JsonIgnore]
    public ModelComparisonSeriesChat? CurrentChat => this.State switch
    {
        ModelComparisonSeriesTaskState.FIRST_CHAT => this.ChatOne,
        ModelComparisonSeriesTaskState.SECOND_CHAT => this.ChatTwo,

        _ => null,
    };

    /// <summary>
    /// Begins a task: the user works in the first chat now.
    /// </summary>
    /// <param name="startedAtUtc">When the user began the task, in UTC.</param>
    /// <param name="firstModelIsChatOne">Whether the first model of the series answers in the first chat, drawn at random by the caller.</param>
    /// <returns>The task.</returns>
    public static ModelComparisonSeriesTask Begin(DateTimeOffset startedAtUtc, bool firstModelIsChatOne) => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = startedAtUtc,
        FirstModelIsChatOne = firstModelIsChatOne,
        ChatOne = new() { ChatId = Guid.NewGuid() },
        ChatTwo = new() { ChatId = Guid.NewGuid() },
        State = ModelComparisonSeriesTaskState.FIRST_CHAT,
    };

    /// <summary>
    /// Ends the work in the first chat: the user works in the second chat now, which starts with the same first message.
    /// </summary>
    /// <param name="startedAtUtc">When the user sent the first message of the first chat, in UTC.</param>
    /// <param name="finishedAtUtc">When the user was done with the first chat, in UTC.</param>
    /// <param name="userMessageCount">How many messages the user wrote in the first chat.</param>
    /// <param name="questionPreview">The beginning of the first message, see ModelComparisonQuestionPreview.</param>
    /// <returns>True when the task was in the first chat; otherwise, nothing changes.</returns>
    public bool FinishFirstChat(DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc, int userMessageCount, string questionPreview)
    {
        if (this.State is not ModelComparisonSeriesTaskState.FIRST_CHAT)
            return false;

        this.ChatOne.StartedAtUtc = startedAtUtc;
        this.ChatOne.FinishedAtUtc = finishedAtUtc;
        this.ChatOne.UserMessageCount = userMessageCount;
        this.QuestionPreview = questionPreview;
        this.State = ModelComparisonSeriesTaskState.SECOND_CHAT;
        return true;
    }

    /// <summary>
    /// Ends the work in the second chat: the user decides now which chat helped more.
    /// </summary>
    /// <param name="startedAtUtc">When the first message of the second chat was sent, in UTC.</param>
    /// <param name="finishedAtUtc">When the user was done with the second chat, in UTC.</param>
    /// <param name="userMessageCount">How many messages the user wrote in the second chat, the first one included.</param>
    /// <returns>True when the task was in the second chat; otherwise, nothing changes.</returns>
    public bool FinishSecondChat(DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc, int userMessageCount)
    {
        if (this.State is not ModelComparisonSeriesTaskState.SECOND_CHAT)
            return false;

        this.ChatTwo.StartedAtUtc = startedAtUtc;
        this.ChatTwo.FinishedAtUtc = finishedAtUtc;
        this.ChatTwo.UserMessageCount = userMessageCount;
        this.State = ModelComparisonSeriesTaskState.DECIDING;
        return true;
    }

    /// <summary>
    /// Takes the decision of the user, which names a chat, and keeps it in terms of the models of the series.
    /// </summary>
    /// <param name="vote">The chat the user chose: COLUMN_A for the first chat, COLUMN_B for the second one.</param>
    /// <param name="decidedAtUtc">When the user decided, in UTC.</param>
    /// <param name="isBlind">Whether the user decided without knowing the models.</param>
    /// <returns>True when the user was deciding; otherwise, nothing changes.</returns>
    public bool Decide(ModelComparisonVote vote, DateTimeOffset decidedAtUtc, bool isBlind)
    {
        if (this.State is not ModelComparisonSeriesTaskState.DECIDING)
            return false;

        this.Vote = this.Order.ToModelChoice(vote);
        this.FinishedAtUtc = decidedAtUtc;
        this.IsBlind = isBlind;
        this.State = ModelComparisonSeriesTaskState.DONE;
        return true;
    }

    /// <summary>
    /// Takes the verdict of the judge about both chats, and keeps it in terms of the models of the series.
    /// </summary>
    /// <param name="verdict">The verdict, in which COLUMN_A names the first chat, see ModelComparisonRunner.JudgeChatsAsync.</param>
    public void TakeVerdict(ModelComparisonJudgeVerdict verdict)
    {
        if (verdict is { Completed: true, Preferred: { } preferred })
        {
            this.JudgeChoice = this.Order.ToModelChoice(preferred);
            this.JudgeReasoning = verdict.Reasoning;
            this.JudgeStatus = ModelComparisonSeriesJudgeStatus.VERDICT;
            return;
        }

        this.SkipJudge(ModelComparisonSeriesJudgeStatus.NO_VERDICT);
    }

    /// <summary>
    /// Notes why the judge has no verdict on this task.
    /// </summary>
    /// <param name="status">Why; anything but VERDICT.</param>
    public void SkipJudge(ModelComparisonSeriesJudgeStatus status)
    {
        this.JudgeChoice = null;
        this.JudgeReasoning = string.Empty;
        this.JudgeStatus = status;
    }

    /// <summary>
    /// Gets the number of the chat in which a model of the series answered.
    /// </summary>
    /// <param name="model">The first or the second model of the series.</param>
    /// <returns>1 for the first chat, 2 for the second one.</returns>
    public int ChatNumberOf(ModelComparisonModelChoice model) => (model is ModelComparisonModelChoice.FIRST) == this.FirstModelIsChatOne ? 1 : 2;
}