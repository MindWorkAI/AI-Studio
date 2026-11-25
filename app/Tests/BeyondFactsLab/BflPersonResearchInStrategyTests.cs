using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the research on single people reaches the step "Strategy" of the BeyondFacts Lab.
/// </summary>
[TestFixture]
public sealed class BflPersonResearchInStrategyTests
{
    private static readonly BflPromptParts PARTS = new("{{RESEARCH}}", "Leitfaden", "Schritt", "Anrede", string.Empty);

    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void OnlyTheStepStrategyCarriesTheResearchOnPeople()
    {
        var forms = Forms("Klimapolitik");
        var people = new[] { Person(forms, "Eva Klein", BflPersonResearchStage.All) };

        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All)
            {
                var prompt = BflPromptComposer.Compose(PARTS, step, forms, [], "Deutsch", people: people);
                if (step is BflStep.STRATEGY)
                {
                    Assert.That(prompt, Does.Contain("<person name=\"Eva Klein\">"));
                    Assert.That(prompt, Does.Contain($"<stufe titel=\"{BflPersonResearchStage.POSITIONS.Title}\" stand=\"2026-11-15\">\nText {BflPersonResearchStage.POSITIONS}\n</stufe>"));
                    Assert.That(prompt, Does.Not.Contain("endete vor ihrer letzten Stufe"));
                }
                else
                {
                    Assert.That(prompt, Does.Not.Contain("Eva Klein"), step.ToString());
                }
            }
        }
    }

    [Test]
    public void AnEventWhichIsNotPublicCarriesNoResearchOnPeople()
    {
        var forms = Forms("Klimapolitik");
        var people = new[] { Person(forms, "Eva Klein", BflPersonResearchStage.All) };
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, BflFieldCatalog.EVENT_NOT_PUBLIC);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, forms, [], "Deutsch", people: people);

        Assert.That(prompt, Does.Not.Contain("Eva Klein"));
    }

    [Test]
    public void AnIncompleteOrOutdatedResearchOnAPersonIsMarked()
    {
        var people = new[] { Person(Forms("Energiepolitik"), "Eva Klein", [BflPersonResearchStage.PROFILE]) };

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, Forms("Klimapolitik"), [], "Deutsch", people: people);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("(Diese Recherche endete vor ihrer letzten Stufe; es fehlen Teile.)"));
            Assert.That(prompt, Does.Contain("(Die öffentlichen Angaben haben sich seit dieser Recherche geändert."));
            Assert.That(prompt, Does.Not.Contain($"titel=\"{BflPersonResearchStage.POSITIONS.Title}\""));
        }
    }

    [Test]
    public void OnlyThePeopleOfTheCurrentListCountInTheOrderOfTheList()
    {
        var forms = Forms("Klimapolitik");
        var list = BflResearch.Of("### Frank Ott\n\n### Eva Klein", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC);
        list.Participants = [.. BflParticipant.ListIn(list.Text)];
        var topics = new Dictionary<BflResearchTopic, BflResearch> { [BflResearchTopic.PARTICIPANTS] = list };
        BflPersonResearch[] stored = [Person(forms, "eva klein", BflPersonResearchStage.All), Person(forms, "Nicht Mehr Gelistet", BflPersonResearchStage.All), Person(forms, "Frank Ott", BflPersonResearchStage.All)];

        var research = BflStepResearch.Of(topics, stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.Topics, Is.SameAs(topics));
            Assert.That(research.People.Select(person => person.Name), Is.EqualTo(new[] { "Frank Ott", "eva klein" }));
            Assert.That(BflStepResearch.Of(new Dictionary<BflResearchTopic, BflResearch>(), stored).People, Is.Empty);
        }
    }

    [Test]
    public void APersonWithoutTextIsLeftOut()
    {
        var forms = Forms("Klimapolitik");
        var empty = new BflPersonResearch { Name = "Eva Klein" };
        empty.Set(BflPersonResearchStage.PROFILE, BflResearch.Of("  ", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC));

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, forms, [], "Deutsch", people: [empty]);

        Assert.That(prompt, Is.EqualTo("Es gibt noch keine Recherche."));
    }

    private static BflPersonResearch Person(IReadOnlyDictionary<BflStep, BflStepFormData> forms, string name, IEnumerable<BflPersonResearchStage> stages)
    {
        var person = new BflPersonResearch { Name = name };
        foreach (var stage in stages)
            person.Set(stage, BflResearch.Of($"Text {stage}", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC));

        return person;
    }

    private static Dictionary<BflStep, BflStepFormData> Forms(string eventTopic)
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.FORMAT, "Podium");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_TOPIC, eventTopic);
        return forms;
    }
}