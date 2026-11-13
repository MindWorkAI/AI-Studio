using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The result of a comparison series, with the names of the models: who won the blind tasks, whether the order of the
/// chats mattered, how often the judge agreed, and how much effort each model took.
/// </summary>
public partial class ModelComparisonSeriesResult : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the statistics of the series.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSeriesStatistics Statistics { get; set; } = null!;

    private IReadOnlyList<ModelComparisonSeriesEffort> Efforts => [this.Statistics.FirstEffort, this.Statistics.SecondEffort];

    private string BlindTasksText => string.Format(ModelComparisonFormat.Culture, T("Tasks decided without knowing the models: {0}"), this.Statistics.Pairing.ComparisonCount.CompactCount());

    private string ChatOrderText => string.Format(
        ModelComparisonFormat.Culture,
        TA("The model in the first chat won {0} of these tasks, the model in the second chat {1}. If the second chat wins much more often, knowing the task better may have helped you there."),
        this.Statistics.WinsOfChatOne.CompactCount(),
        this.Statistics.WinsOfChatTwo.CompactCount());

    private string NotBlindText
    {
        get
        {
            var votes = this.Statistics.NotBlindVotes;
            return string.Format(
                ModelComparisonFormat.Culture,
                TA("After you saw the result, you decided {0} more tasks: {1} won {2}, {3} won {4}, and {5} ended equally. They do not count above, since you knew the models."),
                votes.TotalRunCount.CompactCount(),
                this.Statistics.Pairing.First.Label,
                votes.FirstModelWins.CompactCount(),
                this.Statistics.Pairing.Second.Label,
                votes.SecondModelWins.CompactCount(),
                votes.Ties.CompactCount());
        }
    }
}