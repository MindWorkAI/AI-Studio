using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks that a value written by hand counts only when it names one member of its enum.
/// </summary>
/// <remarks>
/// Enum.TryParse reads a number and several names at once as well. Neither is a mistake it
/// reports: both come back as some value, often one which a member has, so a configuration which
/// meant nothing of the sort gets a choice nobody wrote.
/// </remarks>
[TestFixture]
public sealed class EnumNamesTests
{
    [TestCase("EVERY_MESSAGE")]
    [TestCase("every_message")]
    [TestCase("  Every_Message  ")]
    public void ANameCountsWhateverItsCase(string text)
    {
        Assert.That(EnumNames.TryParse<DataSourceRetrievalMode>(text, out var mode), Is.True);
        Assert.That(mode, Is.EqualTo(DataSourceRetrievalMode.EVERY_MESSAGE));
    }

    [TestCase("1")]
    [TestCase("-1")]
    [TestCase("0x1")]
    public void ANumberDoesNotCountEvenWhenAMemberHasIt(string text)
    {
        Assert.That(EnumNames.TryParse<DataSourceRetrievalMode>(text, out _), Is.False, "Enum.TryParse would read 1 as EVERY_MESSAGE.");
    }

    [Test]
    public void SeveralNamesDoNotCount()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EnumNames.TryParse<DataSourceRetrievalMode>("SEMANTIC_SEARCH, EVERY_MESSAGE", out _), Is.False, "Enum.TryParse would combine them to EVERY_MESSAGE.");
            Assert.That(EnumNames.TryParse<ConfidenceLevel>("HIGH, MEDIUM", out _), Is.False, "Enum.TryParse would combine them to a confidence level above HIGH.");
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("EVERY MESSAGE")]
    public void NoNameDoesNotCount(string? text)
    {
        Assert.That(EnumNames.TryParse<DataSourceRetrievalMode>(text, out var mode), Is.False);
        Assert.That(mode, Is.EqualTo(default(DataSourceRetrievalMode)));
    }

    [Test]
    public void TheEnumMayBeGivenAsAType()
    {
        Assert.That(EnumNames.TryParse(typeof(ConfidenceLevel), "moderate", out var level), Is.True);
        Assert.That(level, Is.EqualTo(ConfidenceLevel.MODERATE));
    }
}