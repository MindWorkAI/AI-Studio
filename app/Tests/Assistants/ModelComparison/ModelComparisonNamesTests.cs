using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks that presets and comparison series keep names which the user can tell apart.
/// </summary>
[TestFixture]
public sealed class ModelComparisonNamesTests
{
    private const int MAX_LENGTH = 60;

    [TestCase("Weekly reports", true)]
    [TestCase("weekly REPORTS", true)]
    [TestCase("  Weekly reports  ", true)]
    [TestCase("Weekly report", false)]
    public void ANameIsTakenRegardlessOfCaseAndSpaces(string name, bool expectedTaken)
    {
        Assert.That(ModelComparisonNames.IsTaken(name, ["Weekly reports", "Daily mails"]), Is.EqualTo(expectedTaken));
    }

    [Test]
    public void ATakenNameWithSpacesAroundItCounts()
    {
        Assert.That(ModelComparisonNames.IsTaken("Weekly reports", [" Weekly reports "]), Is.True);
    }

    [Test]
    public void AFreeNameStaysAsItIs()
    {
        Assert.That(ModelComparisonNames.FreeNameFrom(" Weekly reports ", ["Daily mails"], MAX_LENGTH), Is.EqualTo("Weekly reports"));
    }

    [Test]
    public void ATakenNameGetsTheNextFreeNumber()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonNames.FreeNameFrom("Weekly reports", ["Weekly reports"], MAX_LENGTH), Is.EqualTo("Weekly reports (2)"));
            Assert.That(ModelComparisonNames.FreeNameFrom("Weekly reports", ["Weekly reports", "weekly reports (2)"], MAX_LENGTH), Is.EqualTo("Weekly reports (3)"));
        }
    }

    [Test]
    public void ANumberedNameCountsOnFromItsBase()
    {
        string[] takenNames = ["Weekly reports", "Weekly reports (2)"];
        Assert.That(ModelComparisonNames.FreeNameFrom("Weekly reports (2)", takenNames, MAX_LENGTH), Is.EqualTo("Weekly reports (3)"), "A copy of a copy must not become \"Weekly reports (2) (2)\".");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ASeriesNeedsAName(string? name)
    {
        Assert.That(ModelComparisonNames.SeriesNameIssue(name, []), Is.Not.Null);
    }

    [Test]
    public void ASeriesNameMayNotBeTooLong()
    {
        var longestName = new string('a', ModelComparisonNames.MAX_SERIES_NAME_LENGTH);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonNames.SeriesNameIssue(longestName, []), Is.Null);
            Assert.That(ModelComparisonNames.SeriesNameIssue($"  {longestName}  ", []), Is.Null, "Spaces around the name do not count, since they are not stored.");
            Assert.That(ModelComparisonNames.SeriesNameIssue(longestName + "a", []), Is.Not.Null);
        }
    }

    [Test]
    public void ASeriesNameMayNotBeTakenByAnotherSeries()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonNames.SeriesNameIssue("weekly REPORTS", ["Weekly reports"]), Is.Not.Null);
            Assert.That(ModelComparisonNames.SeriesNameIssue("Weekly reports", ["Daily mails"]), Is.Null);
        }
    }

    [Test]
    public void ALongNameIsShortenedForItsNumber()
    {
        var longName = new string('a', MAX_LENGTH);
        var freeName = ModelComparisonNames.FreeNameFrom(longName, [longName], MAX_LENGTH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(freeName, Has.Length.EqualTo(MAX_LENGTH));
            Assert.That(freeName, Does.EndWith(" (2)"));
        }
    }
}