using System.Globalization;
using System.Reflection;

using AIStudio.Settings;
using AIStudio.Tools.Feedback;
using AIStudio.Tools.Metadata;

namespace AIStudio.Tools.Services;

/// <summary>
/// Decides when the chat asks for the weekly feedback, and sends the answers to the configured feedback server.
/// </summary>
/// <remarks>
/// The chat asks once a week: after the first answer on a Friday, or, when the user did not use the chat
/// on that Friday, after the next answer later on. Rating or skipping the feedback settles the week, and
/// the settings remember it across app sessions. Answers are sent once the user finishes the questions,
/// or as soon as they move on after rating, so a partial answer is not lost.
/// </remarks>
public sealed class FeedbackService(SettingsManager settingsManager, ILogger<FeedbackService> logger)
{
    private static readonly string APP_VERSION = Assembly.GetExecutingAssembly().GetCustomAttribute<MetaDataAttribute>()?.Version ?? string.Empty;
    private static readonly TimeSpan SEND_TIMEOUT = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The time saved or lost in one week can be at most one week.
    /// </summary>
    public const int MAX_SAVED_TIME_MINUTES = 7 * 24 * 60;
    public const int MAX_COMMENT_LENGTH = 2_000;

    /// <summary>
    /// Debug builds let developers enter the feedback server themselves, reset the handled week, and use a short
    /// interval, so they can test the prompt on any day. In release builds, only a configuration plugin sets the server.
    /// </summary>
    #if DEBUG
    public static readonly bool HAS_DEVELOPER_TOOLS = true;
    #else
    public static readonly bool HAS_DEVELOPER_TOOLS = false;
    #endif

    /// <summary>
    /// How often the debug mode asks instead of once a week.
    /// </summary>
    public static readonly TimeSpan DEBUG_INTERVAL = TimeSpan.FromMinutes(20);

    private readonly Lock stateLock = new();
    private FeedbackPromptState state = FeedbackPromptState.NONE;
    private Guid promptChatId = Guid.Empty;
    private DateOnly promptWeek = DateOnly.MinValue;

    /// <summary>
    /// True when the feedback is switched on and the endpoint is a usable HTTP(S) address.
    /// </summary>
    public bool IsEnabled => settingsManager.ConfigurationData.Chat.FeedbackEnabled && this.HasEndpoint;

    /// <summary>
    /// True when a usable feedback server is set, usually by the configuration plugin of an organization.
    /// </summary>
    public bool HasEndpoint => this.TryGetEndpoint(out _);

    /// <summary>
    /// True when the short debug interval replaces the weekly rhythm. Release builds always ask weekly.
    /// </summary>
    public bool UsesDebugInterval => HAS_DEVELOPER_TOOLS && settingsManager.ConfigurationData.Feedback.DebugShortInterval;

    /// <summary>
    /// True when the feedback is switched on and the current week was neither rated nor skipped yet.
    /// In the debug interval, true when the last rating or skip is longer ago than the interval.
    /// </summary>
    public bool IsDue
    {
        get
        {
            if (!this.IsEnabled)
                return false;

            var data = settingsManager.ConfigurationData.Feedback;
            if (this.UsesDebugInterval)
                return DateTime.Now - data.LastHandledAt >= DEBUG_INTERVAL;

            return data.LastHandledWeek < GetWeekOf(DateOnly.FromDateTime(DateTime.Now));
        }
    }

    /// <summary>
    /// The Friday a feedback week starts on: the given day when it is a Friday, otherwise the Friday before.
    /// </summary>
    public static DateOnly GetWeekOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7));

    /// <summary>
    /// The prompt is shown in one chat only: the chat whose answer finished when the feedback became due.
    /// </summary>
    public FeedbackPromptState GetState(Guid chatId)
    {
        if (!this.IsEnabled)
            return FeedbackPromptState.NONE;

        lock (this.stateLock)
            return this.promptChatId == chatId ? this.state : FeedbackPromptState.NONE;
    }

    /// <summary>
    /// Called when an answer in a chat finished. Hides the thanks of the last feedback, and asks when the feedback is due.
    /// </summary>
    public void AnswerFinished(Guid chatId)
    {
        if (chatId == Guid.Empty || !this.IsEnabled)
            return;

        lock (this.stateLock)
        {
            if (this.state is FeedbackPromptState.THANKED)
            {
                this.state = FeedbackPromptState.NONE;
                this.promptChatId = Guid.Empty;
            }

            if (this.state is FeedbackPromptState.NONE && this.IsDue)
            {
                this.state = FeedbackPromptState.ASK;
                this.promptChatId = chatId;
                this.promptWeek = GetWeekOf(DateOnly.FromDateTime(DateTime.Now));
            }
        }
    }

    /// <summary>
    /// The prompt left the screen before the user rated, e.g., because they sent a new message or opened
    /// another chat. The week stays open, so the next finished answer asks again, in whichever chat it is.
    /// </summary>
    public void Release(Guid chatId)
    {
        lock (this.stateLock)
        {
            if (this.promptChatId != chatId || this.state is not FeedbackPromptState.ASK)
                return;

            this.state = FeedbackPromptState.NONE;
            this.promptChatId = Guid.Empty;
        }
    }

    /// <summary>
    /// The prompt left the screen after the user rated, but before they finished all questions.
    /// Sends what they answered so far, without anybody waiting for it.
    /// </summary>
    public void SendInBackground(FeedbackRating rating, int? savedTimeMinutes, FeedbackReason reason, string comment, FeedbackContext context) => _ = Task.Run(async () =>
    {
        try
        {
            await this.SendAsync(rating, savedTimeMinutes, reason, comment, context);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not send the partial feedback.");
        }
    });

    /// <summary>
    /// The user closed the prompt without rating: nothing is sent, and the chat asks again next Friday.
    /// </summary>
    public async Task SkipWeekAsync()
    {
        DateOnly week;
        lock (this.stateLock)
        {
            week = this.promptWeek;
            this.state = FeedbackPromptState.NONE;
            this.promptChatId = Guid.Empty;
        }

        await this.MarkWeekHandledAsync(week);
    }

    public async Task<bool> SendAsync(FeedbackRating rating, int? savedTimeMinutes, FeedbackReason reason, string comment, FeedbackContext context, CancellationToken token = default)
    {
        if (rating is FeedbackRating.NONE || !this.TryGetEndpoint(out var endpoint))
            return false;

        DateOnly week;
        lock (this.stateLock)
            week = this.promptWeek;

        var payload = new FeedbackPayload(
            week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            rating.ToString().ToLowerInvariant(),
            savedTimeMinutes is { } minutes ? Math.Clamp(minutes, -MAX_SAVED_TIME_MINUTES, MAX_SAVED_TIME_MINUTES) : null,
            reason is FeedbackReason.NONE ? string.Empty : reason.ToString().ToLowerInvariant(),
            comment.Trim(),
            context.Provider,
            context.Model,
            APP_VERSION);

        try
        {
            using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(SEND_TIMEOUT);

            using var response = await httpClient.PostAsJsonAsync(endpoint, payload, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("The feedback server '{Endpoint}' answered with status {StatusCode}.", endpoint, (int)response.StatusCode);
                return false;
            }

            lock (this.stateLock)
                this.state = FeedbackPromptState.THANKED;

            await this.MarkWeekHandledAsync(week);
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(e, "Could not send the feedback to '{Endpoint}'.", endpoint);
            return false;
        }
    }

    /// <summary>
    /// Forgets the handled week, so the chat asks again after the next answer. Only available in debug builds.
    /// </summary>
    public async Task ResetWeekAsync()
    {
        if (!HAS_DEVELOPER_TOOLS)
            return;

        lock (this.stateLock)
        {
            this.state = FeedbackPromptState.NONE;
            this.promptChatId = Guid.Empty;
        }

        settingsManager.ConfigurationData.Feedback.LastHandledWeek = DateOnly.MinValue;
        settingsManager.ConfigurationData.Feedback.LastHandledAt = DateTime.MinValue;
        await settingsManager.StoreSettings();
    }

    /// <summary>
    /// Switches between the weekly rhythm and the short debug interval. Only available in debug builds.
    /// </summary>
    public async Task SetDebugIntervalAsync(bool enabled)
    {
        if (!HAS_DEVELOPER_TOOLS)
            return;

        settingsManager.ConfigurationData.Feedback.DebugShortInterval = enabled;
        await settingsManager.StoreSettings();
    }

    private async Task MarkWeekHandledAsync(DateOnly week)
    {
        settingsManager.ConfigurationData.Feedback.LastHandledWeek = week;
        settingsManager.ConfigurationData.Feedback.LastHandledAt = DateTime.Now;
        await settingsManager.StoreSettings();
    }

    private bool TryGetEndpoint(out Uri endpoint)
    {
        var configured = settingsManager.ConfigurationData.Chat.FeedbackEndpoint.Trim();
        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            endpoint = uri;
            return true;
        }

        endpoint = null!;
        return false;
    }
}