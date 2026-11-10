using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks when a confirmed work state of the BeyondFacts Lab counts as outdated.
/// </summary>
/// <remarks>
/// A confirmed work state is outdated as soon as the work state of an earlier step is confirmed for the first time,
/// confirmed anew, or withdrawn. A changed form of its own step gives a weaker notice.
/// </remarks>
[TestFixture]
public sealed class BflStalenessTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> SECTIONS = new(StringComparer.Ordinal) { ["SUMMARY"] = "Text" };

    [Test]
    public void AStepWithoutAConfirmationIsNotConfirmed()
    {
        var workStates = new Dictionary<BflStep, BflWorkState> { [BflStep.KNOWLEDGE] = new() { Draft = new(SECTIONS) } };

        Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.NOT_CONFIRMED));
    }

    [Test]
    public void AConfirmationStaysValidWhileNothingChanges()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        Confirm(workStates, BflStep.KNOWLEDGE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflStaleness.Of(BflStep.FRAMEWORK, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.CONFIRMED));
            Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.CONFIRMED));
        }
    }

    [TestCase(Change.CONFIRMED_ANEW, TestName = "AnEarlierStepConfirmedAnewOutdatesTheLaterOnes")]
    [TestCase(Change.WITHDRAWN, TestName = "AnEarlierStepWithdrawnOutdatesTheLaterOnes")]
    [TestCase(Change.WITHDRAWN_AND_CONFIRMED_AGAIN, TestName = "AnEarlierStepWithdrawnAndConfirmedAgainOutdatesTheLaterOnes")]
    public void AChangeOfAnEarlierStepOutdatesTheLaterOnes(Change change)
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        Confirm(workStates, BflStep.GOALS_AND_ROLE);

        switch (change)
        {
            case Change.CONFIRMED_ANEW:
                Confirm(workStates, BflStep.FRAMEWORK);
                break;

            case Change.WITHDRAWN:
                workStates[BflStep.FRAMEWORK].Withdraw();
                break;

            case Change.WITHDRAWN_AND_CONFIRMED_AGAIN:
                workStates[BflStep.FRAMEWORK].Withdraw();
                Confirm(workStates, BflStep.FRAMEWORK);
                break;
        }

        Assert.That(BflStaleness.Of(BflStep.GOALS_AND_ROLE, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.OUTDATED));
    }

    [Test]
    public void AnEarlierStepConfirmedForTheFirstTimeOutdatesTheLaterOnes()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.STRATEGY);

        Confirm(workStates, BflStep.KNOWLEDGE);

        Assert.That(BflStaleness.Of(BflStep.STRATEGY, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.OUTDATED));
    }

    [Test]
    public void ALaterStepNeverOutdatesAnEarlierOne()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.KNOWLEDGE);

        Confirm(workStates, BflStep.STRATEGY);
        Confirm(workStates, BflStep.STRATEGY);
        workStates[BflStep.STRATEGY].Withdraw();

        Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.CONFIRMED));
    }

    [Test]
    public void ConfirmingAnOutdatedStepAgainMakesItValid()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.GOALS_AND_ROLE);
        Confirm(workStates, BflStep.FRAMEWORK);

        Confirm(workStates, BflStep.GOALS_AND_ROLE);

        Assert.That(BflStaleness.Of(BflStep.GOALS_AND_ROLE, workStates, Forms()), Is.EqualTo(BflWorkStateStatus.CONFIRMED));
    }

    [Test]
    public void ChangedDetailsOfTheStepGiveTheWeakerNotice()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.CONTRIBUTION_SUMMARY, "Erste Fassung");
        Confirm(workStates, BflStep.KNOWLEDGE, form);

        form.Set(BflFieldCatalog.CONTRIBUTION_SUMMARY, "Zweite Fassung");

        Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, Forms((BflStep.KNOWLEDGE, form))), Is.EqualTo(BflWorkStateStatus.DETAILS_CHANGED));
    }

    [Test]
    public void AnOutdatedStepWeighsMoreThanChangedDetails()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        var form = new BflStepFormData();
        Confirm(workStates, BflStep.KNOWLEDGE, form);

        form.Set(BflFieldCatalog.CORE_THESES, "Neu");
        Confirm(workStates, BflStep.FRAMEWORK);

        Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, Forms((BflStep.KNOWLEDGE, form))), Is.EqualTo(BflWorkStateStatus.OUTDATED));
    }

    [Test]
    public void AFormThisVersionCannotOpenChangesNothing()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.CORE_THESES, "These");
        Confirm(workStates, BflStep.KNOWLEDGE, form);

        Assert.That(BflStaleness.Of(BflStep.KNOWLEDGE, workStates, new Dictionary<BflStep, BflStepFormData>()), Is.EqualTo(BflWorkStateStatus.CONFIRMED));
    }

    [Test]
    public void TheFingerprintIgnoresLineEndingsAndSpacesAroundValues()
    {
        var unix = new BflStepFormData();
        unix.Set(BflFieldCatalog.CORE_THESES, "Erste These\nZweite These");
        var windows = new BflStepFormData();
        windows.Set(BflFieldCatalog.CORE_THESES, "  Erste These\r\nZweite These \n");
        var changed = new BflStepFormData();
        changed.Set(BflFieldCatalog.CORE_THESES, "Erste These\nDritte These");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflStaleness.FingerprintOf(windows), Is.EqualTo(BflStaleness.FingerprintOf(unix)));
            Assert.That(BflStaleness.FingerprintOf(changed), Is.Not.EqualTo(BflStaleness.FingerprintOf(unix)));
        }
    }

    [Test]
    public void TheFingerprintTellsWhichFieldHoldsAValue()
    {
        var first = new BflStepFormData();
        first.Set(BflFieldCatalog.CORE_THESES, "Text");
        var second = new BflStepFormData();
        second.Set(BflFieldCatalog.UNCERTAINTIES, "Text");

        Assert.That(BflStaleness.FingerprintOf(first), Is.Not.EqualTo(BflStaleness.FingerprintOf(second)));
    }

    [Test]
    public void AConfirmationRemembersOnlyEarlierSteps()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK);
        Confirm(workStates, BflStep.STRATEGY);
        Confirm(workStates, BflStep.KNOWLEDGE);

        var revisions = BflStaleness.UpstreamRevisionsOf(BflStep.GOALS_AND_ROLE, workStates);

        Assert.That(revisions.Keys, Is.EquivalentTo(new[] { BflStep.FRAMEWORK, BflStep.KNOWLEDGE }));
    }

    [Test]
    public void ARevisionKeepsCountingAfterAWithdrawal()
    {
        var workState = new BflWorkState();
        workState.Confirm(SECTIONS, new Dictionary<BflStep, int>(), string.Empty, NOW);
        workState.Withdraw();
        workState.Confirm(SECTIONS, new Dictionary<BflStep, int>(), string.Empty, NOW);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workState.Confirmed!.Revision, Is.EqualTo(2));
            Assert.That(workState.LastRevision, Is.EqualTo(2));
        }
    }

    [Test]
    public void AConfirmationDropsTheDraftAndEmptySections()
    {
        var workState = new BflWorkState { Draft = new(SECTIONS) };

        workState.Confirm(new Dictionary<string, string> { ["SUMMARY"] = "Text", ["OPEN_POINTS"] = "  " }, new Dictionary<BflStep, int>(), string.Empty, NOW);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workState.Draft, Is.Null);
            Assert.That(workState.Confirmed!.Sections.Keys, Is.EqualTo(new[] { "SUMMARY" }));
        }
    }

    /// <summary>
    /// What happens to an earlier step.
    /// </summary>
    public enum Change
    {
        CONFIRMED_ANEW,
        WITHDRAWN,
        WITHDRAWN_AND_CONFIRMED_AGAIN,
    }

    private static void Confirm(Dictionary<BflStep, BflWorkState> workStates, BflStep step, BflStepFormData? form = null)
    {
        if (!workStates.TryGetValue(step, out var workState))
        {
            workState = new BflWorkState();
            workStates[step] = workState;
        }

        workState.Confirm(SECTIONS, BflStaleness.UpstreamRevisionsOf(step, workStates), BflStaleness.FingerprintOf(form ?? new BflStepFormData()), NOW);
    }

    private static Dictionary<BflStep, BflStepFormData> Forms(params (BflStep Step, BflStepFormData Form)[] forms)
    {
        var result = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        foreach (var (step, form) in forms)
            result[step] = form;

        return result;
    }
}