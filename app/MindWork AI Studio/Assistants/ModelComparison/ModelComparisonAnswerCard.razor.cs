using AIStudio.Chat;
using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The card of one answer of the Model Comparison assistant, with its title above.
/// </summary>
/// <remarks>
/// The same markup for both answers of a run, so that nothing but the position tells them apart. The two cards are in
/// one row of the grid and fill it, so they are equally high however long the answers are. That is why the names of the
/// models are not part of the card: how long a name is would make the cards differ, see ModelComparisonAnswerCaption.
/// </remarks>
public partial class ModelComparisonAnswerCard : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the title: "Answer A" or "Answer B" while the user votes, "First model" or "Second model" in the
    /// summary.
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the answer.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonAnswer Answer { get; set; } = null!;

    /// <summary>
    /// Gets or sets when the run ended. Both answers of a run show this one time: a time of their own would tell the
    /// faster model from the slower one.
    /// </summary>
    [Parameter]
    public DateTimeOffset Time { get; set; }

    /// <summary>
    /// Gets or sets the classes of the card, see ModelComparisonOutcome.CardClassOf.
    /// </summary>
    [Parameter]
    public string CardClass { get; set; } = ModelComparisonOutcome.FILL_THE_COLUMN;

    /// <summary>
    /// The answer as the chat component wants it. Built once per answer, and not on every render: the chat component
    /// compares what it is given to decide whether anything has to be drawn again, and a new one would make it parse the
    /// whole Markdown again.
    /// </summary>
    private ContentText content = new();

    /// <summary>
    /// The answer which content was built from.
    /// </summary>
    private ModelComparisonAnswer? shownAnswer;

    #region Overrides of ComponentBase

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(this.shownAnswer, this.Answer))
        {
            this.shownAnswer = this.Answer;
            this.content = new ContentText { Text = this.Answer.Text };
        }

        base.OnParametersSet();
    }

    #endregion
}