using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the judge said about the two answers of a run, with the names of the models. Shown only in the summary of a
/// batch, so it cannot anchor a vote of the user; without a judge, it shows nothing.
/// </summary>
public partial class ModelComparisonJudgeVerdictCard : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the run, with the verdict of the judge.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonRunResult Result { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model the user preferred in the run, or null without a vote.
    /// </summary>
    [Parameter]
    public ModelComparisonModelChoice? VoteChoice { get; set; }

    /// <summary>
    /// Gets or sets the model the judge preferred in the run, or null without a usable verdict.
    /// </summary>
    [Parameter]
    public ModelComparisonModelChoice? JudgeChoice { get; set; }

    /// <summary>
    /// Names what the judge preferred, by the name of the model, and says whether that matches the vote of the user.
    /// </summary>
    private string PreferenceText
    {
        get
        {
            if (this.JudgeChoice is not { } preferred)
                return string.Empty;

            if (preferred is ModelComparisonModelChoice.TIE)
                return this.VoteChoice switch
                {
                    null => T("The judge saw both answers as equally good."),
                    ModelComparisonModelChoice.TIE => TA("The judge saw both answers as equally good, matching your own vote."),

                    _ => TA("The judge saw both answers as equally good, while you preferred one of them."),
                };

            var preferredName = this.Result.AnswerOf(preferred).Label;
            return this.VoteChoice switch
            {
                null => string.Format(ModelComparisonFormat.Culture, T("The judge preferred {0}."), preferredName),
                { } chosen when chosen == preferred => string.Format(ModelComparisonFormat.Culture, TA("The judge preferred {0}, matching your own vote."), preferredName),

                _ => string.Format(ModelComparisonFormat.Culture, TA("The judge preferred {0}, differing from your own vote."), preferredName),
            };
        }
    }
}