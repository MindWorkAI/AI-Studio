using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The progress of a batch with several runs: one badge per run, which says whether it still runs, waits for the vote,
/// or is done.
/// </summary>
public partial class ModelComparisonRunProgress : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the batch.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonBatch Batch { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether a judge takes part in the batch.
    /// </summary>
    [Parameter]
    public bool JudgeEnabled { get; set; }

    /// <summary>
    /// Says what the badge of a run means, for the tooltip over it: the badge alone only shows a color and a small icon,
    /// not enough to be sure what it stands for.
    /// </summary>
    /// <param name="index">The position of the run, zero-based.</param>
    private string DescribeStatus(int index)
    {
        var text = this.Batch.StatusOf(index) switch
        {
            ModelComparisonRunStatus.QUEUED => T("Run {0}: queued, waiting for a free slot."),
            ModelComparisonRunStatus.RUNNING => T("Run {0}: still working."),
            ModelComparisonRunStatus.NO_USABLE_ANSWER => T("Run {0}: no usable answer, nothing to vote on."),
            ModelComparisonRunStatus.VOTED => T("Run {0}: voted on."),
            ModelComparisonRunStatus.VOTE_SKIPPED => TA("Run {0}: your vote was skipped."),

            _ when this.JudgeEnabled && !this.Batch.HasJudgeVerdict(index) => TA("Run {0}: the judge gave no usable verdict, your vote is missing."),
            _ when this.JudgeEnabled => TA("Run {0}: the models and the judge are done, your vote is missing."),
            _ => TA("Run {0}: the models are done, your vote is missing."),
        };

        return string.Format(ModelComparisonFormat.Culture, text, index + 1);
    }
}