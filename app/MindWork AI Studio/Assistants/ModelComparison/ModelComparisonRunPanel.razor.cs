using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One run in the summary of a batch, with the names of the models: closed, a line with the vote of the user and the
/// choice of the judge; open, both answers, the outcome, the times, and the verdict of the judge with its reasoning.
/// </summary>
/// <remarks>
/// The answers stand in the order of the models, not in the columns of the vote: the columns were drawn fresh for every
/// run, and across several runs, only the names say anything.
/// </remarks>
public partial class ModelComparisonRunPanel : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the batch.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonBatch Batch { get; set; } = null!;

    /// <summary>
    /// Gets or sets the position of the run, zero-based.
    /// </summary>
    [Parameter, EditorRequired]
    public int Index { get; set; }

    private ModelComparisonBatchEntry Entry => this.Batch.Entries[this.Index];

    private ModelComparisonModelChoice? VoteChoice => this.Batch.VoteChoiceAt(this.Index);

    private ModelComparisonModelChoice? JudgeChoice => this.Batch.JudgeChoiceAt(this.Index);

    /// <summary>
    /// Names a model of the run, or says that both answers were equally good.
    /// </summary>
    /// <param name="choice">A model, or a tie.</param>
    private string NameOf(ModelComparisonModelChoice choice) => choice is ModelComparisonModelChoice.TIE
        ? T("Equally good")
        : this.Entry.Result.AnswerOf(choice).Label;

    /// <summary>
    /// Says in the title of the run what the user chose.
    /// </summary>
    private string Heading
    {
        get
        {
            var runNumber = this.Index + 1;
            if (!this.Entry.Result.BothCompleted)
                return string.Format(ModelComparisonFormat.Culture, T("Run {0}: at least one model did not answer"), runNumber);

            return this.VoteChoice switch
            {
                null => string.Format(ModelComparisonFormat.Culture, TA("Run {0}: without your vote"), runNumber),
                ModelComparisonModelChoice.TIE => string.Format(ModelComparisonFormat.Culture, TA("Run {0}: you found both answers equally good"), runNumber),

                { } chosen => string.Format(ModelComparisonFormat.Culture, TA("Run {0}: you preferred {1}"), runNumber, this.NameOf(chosen)),
            };
        }
    }
}