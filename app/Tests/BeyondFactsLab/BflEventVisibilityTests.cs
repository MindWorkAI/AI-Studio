using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks when the BeyondFacts Lab takes an event for not public, and when a suggestion about it keeps the research
/// from starting on its own.
/// </summary>
[TestFixture]
public sealed class BflEventVisibilityTests
{
    [TestCase(null, false, TestName = "AnEmptyFieldLetsTheLabResearch")]
    [TestCase(BflFieldCatalog.EVENT_PUBLIC, false, TestName = "APublicEventIsResearched")]
    [TestCase(BflFieldCatalog.EVENT_NOT_PUBLIC, true, TestName = "AnEventWhichIsNotPublicIsNeverResearched")]
    [TestCase("event_unknown", false, TestName = "AnOptionThisVersionDoesNotKnowLetsTheLabResearch")]
    public void OnlyTheOptionNotPublicStopsTheResearch(string? visibility, bool isNotPublic)
    {
        var forms = Forms(visibility);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflEventVisibility.IsNotPublic(forms), Is.EqualTo(isNotPublic));
            Assert.That(BflEventVisibility.IsNotPublic(forms[BflStep.FRAMEWORK]), Is.EqualTo(isNotPublic));
        }
    }

    [Test]
    public void APreparationWithoutEventDetailsIsResearchedAsBefore()
    {
        Assert.That(BflEventVisibility.IsNotPublic(new Dictionary<BflStep, BflStepFormData>()), Is.False);
    }

    [TestCase(BflFieldCatalog.EVENT_PUBLIC)]
    [TestCase(BflFieldCatalog.EVENT_NOT_PUBLIC)]
    public void ASuggestionWaitsWhileThePersonHasNotDecided(string suggestion)
    {
        Assert.That(BflEventVisibility.IsSuggestionPending(WorkStates(suggestion), Forms(null)), Is.True);
    }

    [Test]
    public void ASuggestionNoLongerWaitsOnceThePersonDecided()
    {
        Assert.That(BflEventVisibility.IsSuggestionPending(WorkStates(BflFieldCatalog.EVENT_NOT_PUBLIC), Forms(BflFieldCatalog.EVENT_PUBLIC)), Is.False);
    }

    [Test]
    public void WithoutASuggestionNothingWaits()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflEventVisibility.IsSuggestionPending(new Dictionary<BflStep, BflWorkState>(), Forms(null)), Is.False);
            Assert.That(BflEventVisibility.IsSuggestionPending(new Dictionary<BflStep, BflWorkState> { [BflStep.FRAMEWORK] = new() }, Forms(null)), Is.False);
        }
    }

    [Test]
    public void ASuggestionWhichThisVersionDoesNotShowKeepsNothingWaiting()
    {
        Assert.That(BflEventVisibility.IsSuggestionPending(WorkStates("event_unknown"), Forms(null)), Is.False);
    }

    private static Dictionary<BflStep, BflStepFormData> Forms(string? visibility)
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_TOPIC, "Klimapolitik");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, visibility);
        return forms;
    }

    private static Dictionary<BflStep, BflWorkState> WorkStates(string suggestion)
    {
        var workState = new BflWorkState();
        workState.FieldSuggestions[BflFieldCatalog.EVENT_VISIBILITY] = suggestion;
        return new Dictionary<BflStep, BflWorkState> { [BflStep.FRAMEWORK] = workState };
    }
}