using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what the chats of the BeyondFacts Lab learn about their tools with every request.
/// </summary>
[TestFixture]
public sealed class BflToolStatusTests
{
    private static readonly BflResearchAvailability EVERYTHING = new(ToolOfferBlockReason.NONE, ToolOfferBlockReason.NONE);

    private static readonly BflResearchAvailability NOTHING = new(ToolOfferBlockReason.NOT_CONFIGURED, ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER);

    [Test]
    public void EveryReasonHasASentenceOfItsOwn()
    {
        var sentences = Enum.GetValues<ToolOfferBlockReason>().Select(BflToolStatus.ReasonFor).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sentences, Has.All.Not.Empty);
            Assert.That(sentences, Is.Unique);
            Assert.That(sentences, Has.None.EqualTo(BflToolStatus.ReasonFor((ToolOfferBlockReason)int.MaxValue)), "Every reason this version knows has a sentence of its own.");
        }
    }

    [Test]
    public void AStepWithItsToolsReadsPagesAndKnowsTheResearch()
    {
        var text = BflToolStatus.ForStep(EVERYTHING);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Du kannst Webseiten lesen"));
            Assert.That(text, Does.Contain("Im Web suchen kannst du in diesem Gespräch nie"));
            Assert.That(text, Does.Contain("Mit der aktuellen Einrichtung ist die Recherche verfügbar."));
        }
    }

    [Test]
    public void AStepWithoutItsToolsLearnsWhy()
    {
        var text = BflToolStatus.ForStep(NOTHING);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Webseiten lesen kannst du in diesem Gespräch nicht."));
            Assert.That(text, Does.Contain(BflToolStatus.ReasonFor(ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER)));
            Assert.That(text, Does.Contain("Mit der aktuellen Einrichtung ist die Recherche nicht verfügbar."));
            Assert.That(text, Does.Contain(BflToolStatus.ReasonFor(ToolOfferBlockReason.NOT_CONFIGURED)));
            Assert.That(text, Does.Not.Contain("Du kannst Webseiten lesen"));
        }
    }

    [Test]
    public void TheOnboardingTellsWhatTheLabCanDoWithTheCurrentSetup()
    {
        var everything = BflToolStatus.ForOnboarding(EVERYTHING);
        var nothing = BflToolStatus.ForOnboarding(NOTHING);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(everything, Does.Contain("kann das Lab im Web recherchieren"));
            Assert.That(everything, Does.Contain("können außerdem Webseiten lesen"));
            Assert.That(nothing, Does.Contain("kann das Lab nicht im Web recherchieren"));
            Assert.That(nothing, Does.Contain(BflToolStatus.ReasonFor(ToolOfferBlockReason.NOT_CONFIGURED)));
            Assert.That(nothing, Does.Contain(BflToolStatus.ReasonFor(ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER)));
        }
    }

    [Test]
    public void EveryTextNamesExactlyTheDetailsTheResearchReceives()
    {
        var texts = new[] { BflToolStatus.ForStep(EVERYTHING), BflToolStatus.ForStep(NOTHING), BflToolStatus.ForOnboarding(EVERYTHING), BflToolStatus.ForOnboarding(NOTHING) };

        using (Assert.EnterMultipleScope())
        {
            foreach (var field in BflFieldCatalog.FIELDS)
            {
                foreach (var text in texts)
                {
                    if (field.IsSharedWithResearch)
                        Assert.That(text, Does.Contain($"„{field.Label}“"), field.Id);
                    else
                        Assert.That(text, Does.Not.Contain($"„{field.Label}“"), field.Id);
                }
            }
        }
    }

    [Test]
    public void TheSameSetupGivesTheSamePrompt()
    {
        var parts = new BflPromptParts("{{TOOLS}}", "Leitfaden", "Schritt", "Anrede");
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());

        var first = BflPromptComposer.Compose(parts, BflStep.STRATEGY, forms, [], "Deutsch", null, new BflResearchAvailability(ToolOfferBlockReason.NONE, ToolOfferBlockReason.NONE));
        var second = BflPromptComposer.Compose(parts, BflStep.STRATEGY, forms, [], "Deutsch", null, new BflResearchAvailability(ToolOfferBlockReason.NONE, ToolOfferBlockReason.NONE));
        var other = BflPromptComposer.Compose(parts, BflStep.STRATEGY, forms, [], "Deutsch", null, NOTHING);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
            Assert.That(first, Does.Not.Contain("\r"));
        }
    }
}