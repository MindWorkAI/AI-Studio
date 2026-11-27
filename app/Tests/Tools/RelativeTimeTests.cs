using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how long ago a time reads, see RelativeTime.Since.
/// </summary>
[TestFixture]
public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private const int MINUTE = 60;
    private const int HOUR = 60 * MINUTE;
    private const int DAY = 24 * HOUR;

    /// <summary>
    /// Each step starts at its border, holds between two borders, and holds up to the second before
    /// the next one: a time counts down to whole units, it never rounds up.
    /// </summary>
    [TestCase(0, "just now")]
    [TestCase(30, "just now")]
    [TestCase(MINUTE - 1, "just now")]
    [TestCase(MINUTE, "a minute ago")]
    [TestCase(MINUTE + 30, "a minute ago")]
    [TestCase(2 * MINUTE - 1, "a minute ago")]
    [TestCase(2 * MINUTE, "2 minutes ago")]
    [TestCase(20 * MINUTE + 5, "20 minutes ago")]
    [TestCase(HOUR - 1, "59 minutes ago")]
    [TestCase(HOUR, "an hour ago")]
    [TestCase(HOUR + 30 * MINUTE, "an hour ago")]
    [TestCase(2 * HOUR - 1, "an hour ago")]
    [TestCase(5 * HOUR, "5 hours ago")]
    [TestCase(5 * HOUR + 17 * MINUTE + 3, "5 hours ago")]
    [TestCase(24 * HOUR - 1, "23 hours ago")]
    public void ShortTimesCountInMinutesAndHours(int secondsAgo, string expected) =>
        Assert.That(RelativeTime.Since(NOW.AddSeconds(-secondsAgo), NOW), Is.EqualTo(expected));

    /// <summary>
    /// Like the short times: a week holds 7 days, a month 30 days from 35 days on, and a year 365 days.
    /// </summary>
    [TestCase(DAY, "a day ago")]
    [TestCase(DAY + 5 * HOUR, "a day ago")]
    [TestCase(2 * DAY - 1, "a day ago")]
    [TestCase(2 * DAY, "2 days ago")]
    [TestCase(3 * DAY + 11 * HOUR, "3 days ago")]
    [TestCase(7 * DAY - 1, "6 days ago")]
    [TestCase(7 * DAY, "a week ago")]
    [TestCase(10 * DAY + 4 * HOUR, "a week ago")]
    [TestCase(14 * DAY, "2 weeks ago")]
    [TestCase(28 * DAY, "4 weeks ago")]
    [TestCase(35 * DAY - 1, "4 weeks ago")]
    [TestCase(35 * DAY, "a month ago")]
    [TestCase(47 * DAY + 6 * HOUR, "a month ago")]
    [TestCase(60 * DAY, "2 months ago")]
    [TestCase(150 * DAY, "5 months ago")]
    [TestCase(200 * DAY + 13 * HOUR, "6 months ago")]
    [TestCase(365 * DAY - 1, "12 months ago")]
    [TestCase(365 * DAY, "a year ago")]
    [TestCase(500 * DAY + 3 * HOUR, "a year ago")]
    [TestCase(2 * 365 * DAY, "2 years ago")]
    [TestCase(3 * 365 * DAY + 100 * DAY, "3 years ago")]
    public void LongTimesCountInDaysWeeksMonthsAndYears(int secondsAgo, string expected) =>
        Assert.That(RelativeTime.Since(NOW.AddSeconds(-secondsAgo), NOW), Is.EqualTo(expected));

    [Test]
    public void ATimeInTheFutureCountsAsJustNow() =>
        Assert.That(RelativeTime.Since(NOW.AddMinutes(5), NOW), Is.EqualTo("just now"));
}