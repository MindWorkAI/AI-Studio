using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs.Settings;

public partial class SettingsDialogChat : SettingsDialogBase
{
    [Inject]
    private FeedbackService FeedbackService { get; init; } = null!;

    /// <summary>
    /// Debug builds only, so the texts stay out of the localization.
    /// </summary>
    private async Task ResetFeedbackWeek()
    {
        await this.FeedbackService.ResetWeekAsync();
        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Feedback, "Debug: the weekly feedback asks again after your next answer."));
    }

    /// <summary>
    /// Debug builds only, so the texts stay out of the localization.
    /// </summary>
    private async Task SetFeedbackDebugInterval(bool enabled) => await this.FeedbackService.SetDebugIntervalAsync(enabled);
}