using AIStudio.Tools.Feedback;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Asks the weekly feedback below the last answer of a chat, one question after the other.
/// </summary>
/// <remarks>
/// When the prompt leaves the screen before the user finished, because they sent a new message or opened
/// another chat, it sends what they answered so far. Without a rating, nothing is sent and the next answer asks again.
/// </remarks>
public partial class ChatFeedbackPrompt : MSGComponentBase
{
    private enum Question
    {
        RATING,
        REASON,
        TIME,
        COMMENT,
    }

    /// <summary>
    /// The chat this prompt is shown in.
    /// </summary>
    [Parameter]
    public Guid ChatId { get; set; }

    /// <summary>
    /// The provider of the chat, to tell the server which AI answered.
    /// </summary>
    [Parameter]
    public AIStudio.Settings.Provider Provider { get; set; } = AIStudio.Settings.Provider.NONE;

    [Inject]
    private FeedbackService FeedbackService { get; init; } = null!;

    private Question step = Question.RATING;
    private FeedbackRating rating = FeedbackRating.NONE;
    private FeedbackReason reason = FeedbackReason.NONE;
    private FeedbackTimeSaved timeSaved = FeedbackTimeSaved.NONE;
    private FeedbackTopic topic = FeedbackTopic.NONE;
    private string comment = string.Empty;
    private bool isSending;
    private bool sendFailed;
    private bool wasSent;

    private string QuestionText => this.step switch
    {
        Question.RATING => this.T("How did AI Studio perform for you this week?"),
        Question.REASON => this.T("What went wrong?"),
        Question.TIME => this.T("Did you save time this week by using AI Studio?"),
        _ => this.T("Is there anything else you would like to tell us?"),
    };

    private string CloseTooltip => this.rating is FeedbackRating.NONE
        ? this.T("Skip this week's feedback")
        : this.T("Send the answers so far and close");

    private FeedbackContext Context => FeedbackContext.From(this.Provider);

    /// <summary>
    /// The answers to the time question, from lost time to much time saved, with "don't know" in the middle.
    /// </summary>
    private IEnumerable<(FeedbackTimeSaved Value, string Label)> TimeSavedOptions =>
    [
        (FeedbackTimeSaved.LOST, this.T("No, I lost time")),
        (FeedbackTimeSaved.NOT_SAVED, this.T("No")),
        (FeedbackTimeSaved.UNKNOWN, this.T("Don't know")),
        (FeedbackTimeSaved.SAVED_SOME, this.T("Yes, a little")),
        (FeedbackTimeSaved.SAVED_MUCH, this.T("Yes, a lot")),
    ];

    private IEnumerable<(FeedbackTopic Value, string Label)> TopicOptions =>
    [
        (FeedbackTopic.TECHNICAL_PROBLEMS, this.T("Technical problems")),
        (FeedbackTopic.NEW_FEATURE, this.T("New feature")),
        (FeedbackTopic.MISSING_FUNCTIONS, this.T("Missing functions")),
        (FeedbackTopic.CONFUSING_UI, this.T("Confusing interface")),
        (FeedbackTopic.OTHER, this.T("Other")),
    ];

    /// <summary>
    /// A bad rating first asks for the reason, before the question about the time.
    /// </summary>
    private void Rate(FeedbackRating selected)
    {
        this.rating = selected;
        this.step = selected is FeedbackRating.BAD ? Question.REASON : Question.TIME;
    }

    private void ChooseReason(FeedbackReason selected)
    {
        this.reason = selected;
        this.step = Question.TIME;
    }

    private void ChooseTimeSaved(FeedbackTimeSaved selected)
    {
        this.timeSaved = selected;
        this.step = Question.COMMENT;
    }

    /// <summary>
    /// Selects a topic, or clears it when the user clicks the selected topic again.
    /// </summary>
    private void ToggleTopic(FeedbackTopic selected) => this.topic = this.topic == selected ? FeedbackTopic.NONE : selected;

    private async Task Close()
    {
        if (this.rating is FeedbackRating.NONE)
        {
            await this.FeedbackService.SkipWeekAsync();
            return;
        }

        await this.Send();
    }

    private async Task Send()
    {
        this.isSending = true;
        this.sendFailed = false;

        this.wasSent = await this.FeedbackService.SendAsync(this.rating, this.timeSaved, this.reason, this.topic, this.comment, this.Context);
        this.sendFailed = !this.wasSent;
        this.isSending = false;
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        if (this.rating is FeedbackRating.NONE)
            this.FeedbackService.Release(this.ChatId);
        else if (!this.wasSent && !this.isSending)
            this.FeedbackService.SendInBackground(this.rating, this.timeSaved, this.reason, this.topic, this.comment, this.Context);

        base.DisposeResources();
    }

    #endregion
}