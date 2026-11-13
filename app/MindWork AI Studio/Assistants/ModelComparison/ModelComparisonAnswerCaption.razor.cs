using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the summary of a batch says about the model of an answer: its name, how the user voted, whether the judge
/// preferred it, and how long it took.
/// </summary>
/// <remarks>
/// Below the answer, not above it: an answer can be long, and what stands above it is out of sight by the time it has
/// been read.
/// </remarks>
public partial class ModelComparisonAnswerCaption : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the answer, which knows who wrote it.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonAnswer Answer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the model whose answer this is.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonModelChoice Model { get; set; }

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
    /// Says how long the answer took: until the first piece arrived, and until it was complete.
    /// </summary>
    private string TimesText => string.Format(
        ModelComparisonFormat.Culture,
        T("First token after {0}, complete after {1}"),
        ModelComparisonFormat.Duration(this.Answer.FirstTokenTime),
        ModelComparisonFormat.Duration(this.Answer.TotalTime));
}