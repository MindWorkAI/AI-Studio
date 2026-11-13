using AIStudio.Chat;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PowerPoint;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// The part of the Slide Assistant which tells, below the progress bar, what the LLM is doing while
/// it plans.
/// </summary>
/// <remarks>
/// The answer is JSON, which the assistant does not show. Without these texts, the user would watch
/// a bar for minutes without knowing whether anything happens: the LLM first thinks about the
/// structure, which streams nothing at all, then writes the slides one by one, and may fit them to
/// the layouts in a second round.
/// </remarks>
public partial class SlideAssistant
{
    private PlanningStep planningStep;
    private static readonly AssistantSessionStateKey<PlanningStep> PLANNING_STEP_STATE_KEY = new(nameof(planningStep));

    private ContentText? countedAnswer;
    private StreamedSlideCounter slideCounter = new();

    protected override string ProgressText => this.planningStep switch
    {
        PlanningStep.PLANNING when this.SlidesSoFar() is var slides and > 0 => string.Format(T("The LLM is writing slide {0}..."), slides),
        PlanningStep.PLANNING => T("The LLM is planning the structure of the presentation..."),
        PlanningStep.REPAIRING => T("The LLM is fitting the slides to the layouts..."),

        _ => string.Empty,
    };

    /// <summary>
    /// Counts the slides which the answer streaming in has started so far.
    /// </summary>
    private int SlidesSoFar()
    {
        if (this.ResultingContentBlock?.Content is not ContentText answer)
            return 0;

        if (!ReferenceEquals(answer, this.countedAnswer))
        {
            this.countedAnswer = answer;
            this.slideCounter = new();
        }

        return this.slideCounter.Count(answer.Text);
    }

    /// <summary>
    /// What the LLM is asked to do right now.
    /// </summary>
    private enum PlanningStep
    {
        NONE,
        PLANNING,
        REPAIRING,
    }
}
