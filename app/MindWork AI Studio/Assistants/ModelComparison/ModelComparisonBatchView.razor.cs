using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Everything which follows the Compare button of the Model Comparison assistant: the wait, the answers, the vote, and
/// the summary.
/// </summary>
/// <remarks>
/// The assistant renders it below its submit row, see AssistantBase.BelowSubmitContent: this is where the button has to
/// stay above the vote, and content of the body would come before it. The batch changes only on the dispatcher of the
/// page, which renders this view anew after every change.
/// </remarks>
public partial class ModelComparisonBatchView : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the batch which runs or ran, or null while the form is shown.
    /// </summary>
    [Parameter]
    public ModelComparisonBatch? Batch { get; set; }

    /// <summary>
    /// Gets or sets whether at least one run of the batch is still in flight.
    /// </summary>
    [Parameter]
    public bool IsComparing { get; set; }

    /// <summary>
    /// Gets or sets whether a judge takes part in the batch.
    /// </summary>
    [Parameter]
    public bool JudgeEnabled { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user votes on the current run.
    /// </summary>
    [Parameter]
    public EventCallback<ModelComparisonVote> Vote { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user skips the vote on the current run.
    /// </summary>
    [Parameter]
    public EventCallback SkipThisRun { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user skips the votes on all remaining runs.
    /// </summary>
    [Parameter]
    public EventCallback SkipRemainingVotes { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user moves past a run which has nothing to vote on.
    /// </summary>
    [Parameter]
    public EventCallback NextEntry { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user wants to start over with the request.
    /// </summary>
    [Parameter]
    public EventCallback StartNewBatch { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user wants to see the results of all comparisons.
    /// </summary>
    [Parameter]
    public EventCallback OpenResults { get; set; }
}