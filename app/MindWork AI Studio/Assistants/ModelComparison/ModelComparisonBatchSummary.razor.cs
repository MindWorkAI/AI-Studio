using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The summary of a finished batch, with the names of the models: the votes of the user and the verdicts of the judge
/// in total, and every run on its own.
/// </summary>
public partial class ModelComparisonBatchSummary : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the finished batch.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonBatch Batch { get; set; } = null!;

    /// <summary>
    /// Gets or sets whether a judge took part in the batch.
    /// </summary>
    [Parameter]
    public bool JudgeEnabled { get; set; }

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

    /// <summary>
    /// The votes of the user in total, counted once per render.
    /// </summary>
    private ModelComparisonTally voteTally = ModelComparisonTally.NONE;

    /// <summary>
    /// The verdicts of the judge in total, or null without a judge.
    /// </summary>
    private ModelComparisonTally? judgeTally;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        this.voteTally = this.Batch.TallyVotes();
        this.judgeTally = this.JudgeEnabled ? this.Batch.TallyJudgeVerdicts() : null;
        base.OnParametersSet();
    }

    #endregion

    private string Heading => this.Batch.RunCount > 1
        ? string.Format(ModelComparisonFormat.Culture, T("All {0} runs are done."), this.Batch.RunCount)
        : T("Done.");
}