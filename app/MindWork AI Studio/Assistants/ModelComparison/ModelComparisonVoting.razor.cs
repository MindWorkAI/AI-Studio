using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The current run of a batch, while the user votes: both answers without the names of the models, in random columns,
/// and the buttons of the vote. A run in which a model did not answer offers only to move on.
/// </summary>
public partial class ModelComparisonVoting : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the batch.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonBatch Batch { get; set; } = null!;

    /// <summary>
    /// Gets or sets the current run of the batch.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonBatchEntry Entry { get; set; } = null!;

    /// <summary>
    /// Gets or sets what happens when the user votes.
    /// </summary>
    [Parameter]
    public EventCallback<ModelComparisonVote> Vote { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user skips the vote on this run.
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

    private string Heading => this.Batch.RunCount > 1
        ? string.Format(ModelComparisonFormat.Culture, T("Run {0} of {1}"), this.Batch.CurrentIndex + 1, this.Batch.RunCount)
        : T("Answers");
}