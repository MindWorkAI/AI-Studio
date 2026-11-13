using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the first day of the period a mailbox is indexed for.
/// </summary>
[TestFixture]
public sealed class MailboxMaxAgeTests
{
    private static readonly DateTimeOffset NOW = new(2026, 10, 2, 9, 30, 0, TimeSpan.FromHours(2));

    [TestCase(MailboxMaxAge.LAST_3_MONTHS, 2026, 7)]
    [TestCase(MailboxMaxAge.LAST_6_MONTHS, 2026, 4)]
    [TestCase(MailboxMaxAge.LAST_12_MONTHS, 2025, 10)]
    [TestCase(MailboxMaxAge.LAST_24_MONTHS, 2024, 10)]
    [TestCase((MailboxMaxAge)42, 2026, 7)]
    public void ThePeriodStartsMonthsBack(MailboxMaxAge maxAge, int year, int month) =>
        Assert.That(maxAge.GetReceivedSince(NOW), Is.EqualTo(new DateTimeOffset(year, month, 2, 9, 30, 0, TimeSpan.FromHours(2))));

    [Test]
    public void AllMailsHaveNoFirstDay() => Assert.That(MailboxMaxAge.ALL.GetReceivedSince(NOW), Is.Null);
}