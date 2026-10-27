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
        TIME,
        REASON,
        COMMENT,
    }

    private const double MAX_HOURS = FeedbackService.MAX_SAVED_TIME_MINUTES / 60.0;

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
    private bool timeLost;
    private double hours;
    private int? savedTimeMinutes;
    private FeedbackReason reason = FeedbackReason.NONE;
    private string comment = string.Empty;
    private bool isSending;
    private bool sendFailed;
    private bool wasSent;

    /// <summary>
    /// A bad rating adds the question for the reason.
    /// </summary>
    private int QuestionCount => this.rating is FeedbackRating.BAD ? 4 : 3;

    private int QuestionNumber => this.step switch
    {
        Question.RATING => 1,
        Question.TIME => 2,
        Question.REASON => 3,
        _ => this.QuestionCount,
    };

    private string QuestionText => this.step switch
    {
        Question.RATING => this.T("How did AI Studio perform for you this week?"),
        Question.TIME => this.T("How much time did AI Studio save you this week?"),
        Question.REASON => this.T("What went wrong?"),
        _ => this.T("Is there anything else you would like to tell us?"),
    };

    private string CloseTooltip => this.rating is FeedbackRating.NONE
        ? this.T("Skip this week's feedback")
        : this.T("Send the answers so far and close");

    private FeedbackContext Context => FeedbackContext.From(this.Provider);

    private void Rate(FeedbackRating selected)
    {
        this.rating = selected;
        this.step = Question.TIME;
    }

    private void ConfirmTime()
    {
        var minutes = (int)Math.Round(Math.Clamp(this.hours, 0, MAX_HOURS) * 60);
        this.savedTimeMinutes = this.timeLost ? -minutes : minutes;
        this.step = this.NextAfterTime;
    }

    private void SkipTime()
    {
        this.savedTimeMinutes = null;
        this.step = this.NextAfterTime;
    }

    private Question NextAfterTime => this.rating is FeedbackRating.BAD ? Question.REASON : Question.COMMENT;

    private void ChooseReason(FeedbackReason selected)
    {
        this.reason = selected;
        this.step = Question.COMMENT;
    }

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

        this.wasSent = await this.FeedbackService.SendAsync(this.rating, this.savedTimeMinutes, this.reason, this.comment, this.Context);
        this.sendFailed = !this.wasSent;
        this.isSending = false;
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        if (this.rating is FeedbackRating.NONE)
            this.FeedbackService.Release(this.ChatId);
        else if (!this.wasSent && !this.isSending)
            this.FeedbackService.SendInBackground(this.rating, this.savedTimeMinutes, this.reason, this.comment, this.Context);

        base.DisposeResources();
    }

    #endregion
}