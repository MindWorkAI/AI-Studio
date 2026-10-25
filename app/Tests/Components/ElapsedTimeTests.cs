using AIStudio.Components;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks how the time below the progress bar of an assistant is written.
/// </summary>
/// <remarks>
/// Minutes and seconds suffice for almost every run; the hours come in front only once there are any,
/// and they keep counting past a day instead of starting over.
/// </remarks>
[TestFixture]
public sealed class ElapsedTimeTests
{
    [TestCase(0, ExpectedResult = "0:00")]
    [TestCase(9, ExpectedResult = "0:09")]
    [TestCase(61, ExpectedResult = "1:01")]
    [TestCase(59 * 60 + 59, ExpectedResult = "59:59")]
    [TestCase(60 * 60, ExpectedResult = "1:00:00")]
    [TestCase(25 * 60 * 60 + 3 * 60 + 4, ExpectedResult = "25:03:04")]
    public string ATimeSpanIsWrittenAsMinutesAndSecondsWithHoursWhereThereAreAny(int seconds) => ElapsedTime.Format(TimeSpan.FromSeconds(seconds));

    [Test]
    public void ANegativeTimeSpanCountsAsNone() => Assert.That(ElapsedTime.Format(TimeSpan.FromSeconds(-5)), Is.EqualTo("0:00"), "A clock which was set back must not show a negative time.");

    [Test]
    public void FractionsOfASecondAreCutOff() => Assert.That(ElapsedTime.Format(TimeSpan.FromMilliseconds(1_999)), Is.EqualTo("0:01"));
}