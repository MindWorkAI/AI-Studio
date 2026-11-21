using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the research of the BeyondFacts Lab on one person: its stages, its ID, and when it is outdated.
/// </summary>
[TestFixture]
public sealed class BflPersonResearchTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly BflResearchDetail[] DETAILS =
    [
        new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" },
    ];

    [Test]
    public void TheStagesKeepTheirOrderWhateverOrderTheyAreSetIn()
    {
        var research = new BflPersonResearch { Name = "Eva Klein" };
        research.Set(BflPersonResearchStage.APPEARANCES, Research("Auftreten", 3));
        research.Set(BflPersonResearchStage.PROFILE, Research("Profil", 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.Stages.Select(stage => stage.Stage), Is.EqualTo(new[] { BflPersonResearchStage.PROFILE, BflPersonResearchStage.APPEARANCES }));
            Assert.That(research.StageOf(BflPersonResearchStage.PROFILE)!.Text, Is.EqualTo("Profil"));
            Assert.That(research.StageOf(BflPersonResearchStage.POSITIONS), Is.Null);
            Assert.That(research.IsComplete, Is.False);
            Assert.That(research.LastCreatedAtUtc, Is.EqualTo(CREATED_AT_UTC.AddMinutes(3)));
        }
    }

    [Test]
    public void AStageSetAgainReplacesWhatItFoundBefore()
    {
        var research = new BflPersonResearch { Name = "Eva Klein" };
        research.Set(BflPersonResearchStage.POSITIONS, Research("Alt", 1));
        research.Set(BflPersonResearchStage.POSITIONS, Research("Neu", 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.Stages, Has.Count.EqualTo(1));
            Assert.That(research.StageOf(BflPersonResearchStage.POSITIONS)!.Text, Is.EqualTo("Neu"));
        }
    }

    [Test]
    public void AResearchWithEveryStageIsComplete()
    {
        var research = new BflPersonResearch { Name = "Eva Klein" };
        foreach (var stage in BflPersonResearchStage.All)
            research.Set(stage, Research(stage.FileStem, 1));

        Assert.That(research.IsComplete, Is.True);
    }

    [Test]
    public void AResearchWithoutStagesHasNoTime()
    {
        Assert.That(new BflPersonResearch { Name = "Eva Klein" }.LastCreatedAtUtc, Is.Null);
    }

    [Test]
    public void AResearchIsOutdatedOnceOneOfItsStagesIs()
    {
        var research = new BflPersonResearch { Name = "Eva Klein" };
        research.Set(BflPersonResearchStage.PROFILE, Research("Profil", 1));

        BflResearchDetail[] changed = [new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Energiepolitik" }];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.IsOutdated(DETAILS), Is.False);
            Assert.That(research.IsOutdated(changed), Is.True);
        }
    }

    [Test]
    public void TheIdOfAPersonFollowsFromTheNameAlone()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflPersonId.Of("Eva Klein"), Is.EqualTo(BflPersonId.Of(" eva   KLEIN ")));
            Assert.That(BflPersonId.Of("Eva Klein"), Is.Not.EqualTo(BflPersonId.Of("Eva Kleine")));
            Assert.That(BflPersonId.Of("Eva Klein").Version, Is.EqualTo(8));
            Assert.That(BflPersonId.Of("Eva Klein"), Is.Not.EqualTo(BflThreadId.ONBOARDING));
        }
    }

    [Test]
    public void EveryStageHasATitleADescriptionAndAFile()
    {
        foreach (var stage in BflPersonResearchStage.All)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(stage.Title, Is.Not.Empty);
                Assert.That(stage.Description, Is.Not.Empty);
                Assert.That(stage.FileStem, Is.Not.Empty);
            }
        }
    }

    private static BflResearch Research(string text, int minutes) => BflResearch.Of(text, [], [], DETAILS, CREATED_AT_UTC.AddMinutes(minutes));
}