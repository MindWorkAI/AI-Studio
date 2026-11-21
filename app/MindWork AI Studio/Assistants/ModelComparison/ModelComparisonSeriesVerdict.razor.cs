using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What the judge of a comparison series said about a decided task, or why it said nothing.
/// </summary>
/// <remarks>
/// Shown only after the decision of the user, so that the judge cannot anchor it. The judge names the chats as the
/// user saw them, "Chat 1" and "Chat 2", never a model.
/// </remarks>
public partial class ModelComparisonSeriesVerdict : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the decided task.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSeriesTask SeriesTask { get; set; } = null!;

    /// <summary>
    /// Gets or sets the number of the task in its series, from 1.
    /// </summary>
    [Parameter, EditorRequired]
    public int TaskNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the judge reads the chats of the task right now.
    /// </summary>
    [Parameter]
    public bool IsJudging { get; set; }

    /// <summary>
    /// Gets or sets what happens when the user asks the judge again, e.g., after a judge without a usable verdict.
    /// </summary>
    [Parameter]
    public EventCallback AskJudge { get; set; }

    /// <summary>
    /// Names the chat the judge preferred, and says whether that matches the decision of the user.
    /// </summary>
    private string VerdictText
    {
        get
        {
            var vote = this.SeriesTask.Vote;
            if (this.SeriesTask.JudgeChoice is not { } judgeChoice)
                return string.Empty;

            if (judgeChoice is ModelComparisonModelChoice.TIE)
                return vote is ModelComparisonModelChoice.TIE
                    ? TA("The judge found both chats equally helpful, as you did.")
                    : TA("The judge found both chats equally helpful, while you preferred one of them.");

            var chatNumber = this.SeriesTask.ChatNumberOf(judgeChoice);
            var text = vote switch
            {
                ModelComparisonModelChoice.TIE => TA("The judge preferred chat {0}, while you found both chats equally helpful."),
                { } chosen when chosen == judgeChoice => TA("The judge preferred chat {0}, as you did."),

                _ => TA("The judge preferred chat {0}, while you preferred the other one."),
            };

            return string.Format(ModelComparisonFormat.Culture, text, chatNumber);
        }
    }
}