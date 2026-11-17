using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the research reaches the step "Strategy" of the BeyondFacts Lab, and when the lab starts it on its own.
/// </summary>
[TestFixture]
public sealed class BflResearchInStrategyTests
{
    private static readonly BflPromptParts PARTS = new("{{RESEARCH}}", "Leitfaden", "Schritt", "Anrede");

    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);

    [Test]
    public void OnlyTheStepStrategyCarriesTheResearch()
    {
        var forms = Forms("Klimapolitik");
        var research = Research(forms, BflResearchTopic.PARTICIPANTS, "## Teilnehmende\n\n### Person A");

        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All)
            {
                var prompt = BflPromptComposer.Compose(PARTS, step, forms, [], "Deutsch", null, null, research);
                if (step is BflStep.STRATEGY)
                {
                    Assert.That(prompt, Does.Contain("### Person A"));
                    Assert.That(prompt, Does.Contain($"<thema titel=\"{BflResearchTopic.PARTICIPANTS.Title}\" stand=\"2026-11-10\">"));
                }
                else
                {
                    Assert.That(prompt, Does.Not.Contain("Person A"), step.ToString());
                    Assert.That(prompt, Does.Contain("„Strategie“"), step.ToString());
                }
            }
        }
    }

    [Test]
    public void TheResearchComesInTheOrderOfItsTopics()
    {
        var forms = Forms("Klimapolitik");
        var research = new Dictionary<BflResearchTopic, BflResearch>
        {
            [BflResearchTopic.DEBATE] = BflResearch.Of("Debatte", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC),
            [BflResearchTopic.EVENT] = BflResearch.Of("Veranstaltung", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC),
        };

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, forms, [], "Deutsch", null, null, research);

        Assert.That(prompt.IndexOf("Veranstaltung", StringComparison.Ordinal), Is.LessThan(prompt.IndexOf("Debatte", StringComparison.Ordinal)));
    }

    [Test]
    public void AResearchWhosePublicDetailsChangedIsMarked()
    {
        var research = Research(Forms("Klimapolitik"), BflResearchTopic.EVENT, "## Veranstaltung");

        var current = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, Forms("Klimapolitik"), [], "Deutsch", null, null, research);
        var changed = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, Forms("Energiepolitik"), [], "Deutsch", null, null, research);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(current, Does.Not.Contain("haben sich seit dieser Recherche geändert"));
            Assert.That(changed, Does.Contain("haben sich seit dieser Recherche geändert"));
        }
    }

    [Test]
    public void ForAnEventWhichIsNotPublicTheStepLearnsThatThereIsNoResearch()
    {
        var forms = Forms("Klimapolitik");
        var research = Research(forms, BflResearchTopic.PARTICIPANTS, "## Teilnehmende\n\n### Person A");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, BflFieldCatalog.EVENT_NOT_PUBLIC);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, forms, [], "Deutsch", null, null, research);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("nicht öffentlich"));
            Assert.That(prompt, Does.Not.Contain("Person A"), "An earlier research no longer counts once the person said that the event is not public.");
        }
    }

    [Test]
    public void ForAPublicEventTheStepGetsTheResearch()
    {
        var forms = Forms("Klimapolitik");
        var research = Research(forms, BflResearchTopic.PARTICIPANTS, "## Teilnehmende\n\n### Person A");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, BflFieldCatalog.EVENT_PUBLIC);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, forms, [], "Deutsch", null, null, research);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("### Person A"));
            Assert.That(prompt, Does.Not.Contain("haben sich seit dieser Recherche geändert"), "Whether the event is public is no detail which the research receives.");
        }
    }

    [Test]
    public void WithoutResearchTheStepLearnsThatThereIsNone()
    {
        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, Forms("Klimapolitik"), [], "Deutsch");

        Assert.That(prompt, Is.EqualTo("Es gibt noch keine Recherche."));
    }

    [Test]
    public void EveryTopicWithoutResearchIsDue()
    {
        var details = BflPromptComposer.PublicDetailsOf(Forms("Klimapolitik"));

        Assert.That(BflResearchRunner.TopicsDue(new Dictionary<BflResearchTopic, BflResearch>(), details), Is.EqualTo(BflResearchTopic.All));
    }

    [Test]
    public void OnlyTheTopicsWithoutAFittingResearchAreDue()
    {
        var forms = Forms("Klimapolitik");
        var details = BflPromptComposer.PublicDetailsOf(forms);
        var research = new Dictionary<BflResearchTopic, BflResearch>
        {
            [BflResearchTopic.EVENT] = BflResearch.Of("Veranstaltung", [], [], details, CREATED_AT_UTC),
            [BflResearchTopic.PARTICIPANTS] = BflResearch.Of("Teilnehmende", [], [], BflPromptComposer.PublicDetailsOf(Forms("Energiepolitik")), CREATED_AT_UTC),
        };

        Assert.That(BflResearchRunner.TopicsDue(research, details), Is.EqualTo(new[] { BflResearchTopic.PARTICIPANTS, BflResearchTopic.DEBATE }));
    }

    private static Dictionary<BflStep, BflStepFormData> Forms(string eventTopic)
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.FORMAT, "Podium");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_TOPIC, eventTopic);
        return forms;
    }

    private static Dictionary<BflResearchTopic, BflResearch> Research(IReadOnlyDictionary<BflStep, BflStepFormData> forms, BflResearchTopic topic, string text) => new()
    {
        [topic] = BflResearch.Of(text, [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC),
    };
}