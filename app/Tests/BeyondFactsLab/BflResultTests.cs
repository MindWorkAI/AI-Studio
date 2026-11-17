using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the results of the BeyondFacts Lab: how a draft becomes the confirmed version, and when a result is
/// outdated.
/// </summary>
/// <remarks>
/// A result is outdated as soon as the work state of any step is confirmed for the first time, confirmed anew, or
/// withdrawn after the lab wrote it.
/// </remarks>
[TestFixture]
public sealed class BflResultTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> SECTIONS = new(StringComparer.Ordinal) { ["GOAL"] = "Informieren" };

    [Test]
    public void AConfirmationKeepsWhatTheDraftWasBuiltFrom()
    {
        var result = new BflResult { Draft = new BflResultVersion { Text = "Entwurf", WorkStateRevisions = { [BflStep.FRAMEWORK] = 2 } } };

        result.Confirm("Bestätigt", NOW);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Draft, Is.Null);
            Assert.That(result.Confirmed!.Text, Is.EqualTo("Bestätigt"));
            Assert.That(result.Confirmed.WorkStateRevisions, Is.EqualTo(new Dictionary<BflStep, int> { [BflStep.FRAMEWORK] = 2 }));
            Assert.That(result.Confirmed.ChangedAtUtc, Is.EqualTo(NOW));
        }
    }

    [Test]
    public void AResultWithoutADraftCannotBeConfirmed()
    {
        Assert.Throws<InvalidOperationException>(() => new BflResult().Confirm("Text", NOW));
    }

    [Test]
    public void EditingAConfirmedResultStartsADraftAndKeepsTheConfirmedVersion()
    {
        var result = new BflResult { Confirmed = new BflResultVersion { Text = "Bestätigt", WorkStateRevisions = { [BflStep.FRAMEWORK] = 1 } } };

        result.Edit();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Draft!.Text, Is.EqualTo("Bestätigt"));
            Assert.That(result.Draft.WorkStateRevisions, Is.EqualTo(result.Confirmed.WorkStateRevisions));
            Assert.That(result.Draft.WorkStateRevisions, Is.Not.SameAs(result.Confirmed.WorkStateRevisions));
            Assert.That(result.Confirmed.Text, Is.EqualTo("Bestätigt"));
        }
    }

    [Test]
    public void EditingKeepsADraftWhichIsThereAlready()
    {
        var result = new BflResult
        {
            Draft = new BflResultVersion { Text = "Entwurf" },
            Confirmed = new BflResultVersion { Text = "Bestätigt" },
        };

        result.Edit();

        Assert.That(result.Draft!.Text, Is.EqualTo("Entwurf"));
    }

    [Test]
    public void AResultFitsWhileTheWorkStatesStayAsTheyAre()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        Confirm(workStates, BflStep.TEST_SITUATION);

        var version = new BflResultVersion { WorkStateRevisions = new(BflStaleness.ConfirmedRevisionsOf(workStates)) };

        Assert.That(BflStaleness.IsOutdated(version, workStates), Is.False);
    }

    [TestCase(Change.CONFIRMED_FOR_THE_FIRST_TIME, TestName = "AStepConfirmedForTheFirstTimeOutdatesAResult")]
    [TestCase(Change.CONFIRMED_ANEW, TestName = "AStepConfirmedAnewOutdatesAResult")]
    [TestCase(Change.WITHDRAWN, TestName = "AStepWithdrawnOutdatesAResult")]
    [TestCase(Change.CONFIRMED_ANEW_IN_THE_LAST_STEP, TestName = "TheLastStepConfirmedAnewOutdatesAResult")]
    public void AChangedWorkStateOutdatesAResult(Change change)
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        Confirm(workStates, BflStep.RESULTS);
        var version = new BflResultVersion { WorkStateRevisions = new(BflStaleness.ConfirmedRevisionsOf(workStates)) };

        switch (change)
        {
            case Change.CONFIRMED_FOR_THE_FIRST_TIME:
                Confirm(workStates, BflStep.STRATEGY);
                break;

            case Change.CONFIRMED_ANEW:
                Confirm(workStates, BflStep.FRAMEWORK);
                break;

            case Change.WITHDRAWN:
                workStates[BflStep.FRAMEWORK].Withdraw();
                break;

            case Change.CONFIRMED_ANEW_IN_THE_LAST_STEP:
                Confirm(workStates, BflStep.RESULTS);
                break;
        }

        Assert.That(BflStaleness.IsOutdated(version, workStates), Is.True);
    }

    [Test]
    public void ADraftOfAWorkStateDoesNotOutdateAResult()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        var version = new BflResultVersion { WorkStateRevisions = new(BflStaleness.ConfirmedRevisionsOf(workStates)) };

        workStates[BflStep.FRAMEWORK].Draft = new(SECTIONS);
        workStates[BflStep.KNOWLEDGE] = new BflWorkState { Draft = new(SECTIONS) };

        Assert.That(BflStaleness.IsOutdated(version, workStates), Is.False);
    }

    [Test]
    public void TheConfirmedRevisionsNameEveryStepWithAConfirmedWorkState()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.KNOWLEDGE);
        Confirm(workStates, BflStep.RESULTS);
        Confirm(workStates, BflStep.RESULTS);
        workStates[BflStep.STRATEGY] = new BflWorkState { Draft = new(SECTIONS) };

        Assert.That(BflStaleness.ConfirmedRevisionsOf(workStates), Is.EquivalentTo(new Dictionary<BflStep, int> { [BflStep.KNOWLEDGE] = 1, [BflStep.RESULTS] = 2 }));
    }

    [Test]
    public void EveryResultHasAFileOfItsOwn()
    {
        Assert.That(BflResultKind.All.Select(kind => kind.FileName), Is.Unique);
    }

    [Test]
    public void OnlyTheReflectionFolderIsPutTogetherWithoutAModel()
    {
        Assert.That(BflResultKind.All.Where(kind => !kind.IsWrittenByModel), Is.EqualTo(new[] { BflResultKind.REFLECTION_FOLDER }));
    }

    /// <summary>
    /// What happens to the work states after the lab wrote a result.
    /// </summary>
    public enum Change
    {
        CONFIRMED_FOR_THE_FIRST_TIME,
        CONFIRMED_ANEW,
        WITHDRAWN,
        CONFIRMED_ANEW_IN_THE_LAST_STEP,
    }

    private static void Confirm(Dictionary<BflStep, BflWorkState> workStates, BflStep step)
    {
        if (!workStates.TryGetValue(step, out var workState))
        {
            workState = new BflWorkState();
            workStates[step] = workState;
        }

        workState.Confirm(SECTIONS, BflStaleness.UpstreamRevisionsOf(step, workStates), BflStaleness.FingerprintOf(new BflStepFormData()), NOW);
    }
}