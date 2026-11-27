using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

/// <summary>
/// Tells how long ago something happened, in the words a person would use: just now, minutes, hours,
/// days, weeks, months, or years ago.
/// </summary>
/// <remarks>
/// What shows such a time listens to <see cref="MinutePassed"/> and renders anew, so that "just now"
/// becomes "a minute ago" on its own. One timer serves all of them.
/// </remarks>
public static class RelativeTime
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(RelativeTime).Namespace, nameof(RelativeTime));

    /// <summary>
    /// Raises <see cref="MinutePassed"/> once a minute. The field keeps the timer alive for as long as
    /// the app runs; it starts with the first listener, which touches this class.
    /// </summary>
    private static readonly Timer MINUTE_TIMER = new(_ => MinutePassed?.Invoke(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

    /// <summary>
    /// Raised once a minute, on a thread of the timer: a component renders anew through its own InvokeAsync.
    /// </summary>
    public static event Action? MinutePassed;

    /// <summary>
    /// How long ago the given time was, such as "20 minutes ago" or "2 years ago". A time in the
    /// future, from a clock which runs ahead, counts as just now.
    /// </summary>
    /// <param name="time">The time it happened.</param>
    /// <param name="now">The time to count from.</param>
    public static string Since(DateTimeOffset time, DateTimeOffset now)
    {
        var elapsed = now - time;
        if (elapsed < TimeSpan.FromMinutes(1))
            return TB("just now");

        if (elapsed < TimeSpan.FromHours(1))
            return Count((int)elapsed.TotalMinutes, TB("a minute ago"), TB("{0} minutes ago"));

        if (elapsed < TimeSpan.FromDays(1))
            return Count((int)elapsed.TotalHours, TB("an hour ago"), TB("{0} hours ago"));

        var days = (int)elapsed.TotalDays;
        if (days < 7)
            return Count(days, TB("a day ago"), TB("{0} days ago"));

        if (days < 35)
            return Count(days / 7, TB("a week ago"), TB("{0} weeks ago"));

        if (days < 365)
            return Count(Math.Max(1, days / 30), TB("a month ago"), TB("{0} months ago"));

        return Count(days / 365, TB("a year ago"), TB("{0} years ago"));
    }

    private static string Count(int count, string one, string many) => count == 1 ? one : string.Format(I18N.I.Culture, many, count);
}