using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Models;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks whom the chat of the step "Test situation" of the BeyondFacts Lab may play, whom it plays of what the person
/// chose, and whether a model reads enough for it.
/// </summary>
[TestFixture]
public sealed class BflSimulationTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void OnlyAListedPersonWithDocumentedPositionsQualifies()
    {
        var forms = Forms();
        BflPersonResearch[] people =
        [
            Person(forms, "Eva Klein", BflPersonResearchStage.All),
            Person(forms, "Frank Ott", [BflPersonResearchStage.PROFILE]),
            Person(forms, "Gina Roth", [BflPersonResearchStage.PROFILE, BflPersonResearchStage.POSITIONS], positionsText: "  "),
            Person(forms, "Nicht Gelistet", BflPersonResearchStage.All)
        ];

        var candidates = BflSimulationCandidates.Of(forms, Topics(forms, "Eva Klein", "Frank Ott", "Gina Roth"), people);

        Assert.That(candidates.Select(person => person.Name), Is.EqualTo(new[] { "Eva Klein" }));
    }

    [Test]
    public void TheCandidatesComeInTheOrderOfTheList()
    {
        var forms = Forms();
        BflPersonResearch[] people = [Person(forms, "Eva Klein", BflPersonResearchStage.All), Person(forms, "Frank Ott", BflPersonResearchStage.All)];

        var candidates = BflSimulationCandidates.Of(forms, Topics(forms, "Frank Ott", "Eva Klein"), people);

        Assert.That(candidates.Select(person => person.Name), Is.EqualTo(new[] { "Frank Ott", "Eva Klein" }));
    }

    [Test]
    public void NobodyQualifiesForAnEventWhichIsNotPublic()
    {
        var forms = Forms();
        BflPersonResearch[] people = [Person(forms, "Eva Klein", BflPersonResearchStage.All)];
        var topics = Topics(forms, "Eva Klein");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, BflFieldCatalog.EVENT_NOT_PUBLIC);

        Assert.That(BflSimulationCandidates.Of(forms, topics, people), Is.Empty);
    }

    [Test]
    public void GenericCounterpartsPlayNobody()
    {
        var candidates = Candidates("Eva Klein");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflEffectiveSimulation.Of(null, candidates), Is.SameAs(BflEffectiveSimulation.GENERIC));
            Assert.That(BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.GENERIC, People = ["Eva Klein"] }, candidates), Is.SameAs(BflEffectiveSimulation.GENERIC));
        }
    }

    [TestCase(BflSimulationMode.SINGLE_PERSON, 1)]
    [TestCase(BflSimulationMode.PANEL_DISCUSSION, 3)]
    [TestCase(BflSimulationMode.PRESS_CONFERENCE, 5)]
    public void AModePlaysAtMostItsNumberOfPeopleInTheOrderOfTheList(BflSimulationMode mode, int expected)
    {
        string[] names = ["Anna Arendt", "Bruno Berg", "Clara Cramer", "David Dorn", "Eva Klein", "Frank Ott"];
        var candidates = Candidates(names);
        var simulation = new BflSimulation { Mode = mode, People = [.. names.Reverse()] };

        var effective = BflEffectiveSimulation.Of(simulation, candidates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mode.MaxPeople, Is.EqualTo(expected));
            Assert.That(effective.Mode, Is.EqualTo(mode));
            Assert.That(effective.People.Select(person => person.Name), Is.EqualTo(names.Take(expected)));
        }
    }

    [Test]
    public void APersonWhoIsNoLongerACandidateCountsNoLonger()
    {
        var candidates = Candidates("Eva Klein", "Frank Ott");
        var simulation = new BflSimulation { Mode = BflSimulationMode.PANEL_DISCUSSION, People = ["Nicht Mehr Gelistet", "frank  ott"] };

        var effective = BflEffectiveSimulation.Of(simulation, candidates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(effective.People.Select(person => person.Name), Is.EqualTo(new[] { "Frank Ott" }));
            Assert.That(simulation.People, Has.Count.EqualTo(2), "What the person chose stays stored.");
        }
    }

    [Test]
    public void AModeWithoutAnyChosenCandidateFallsBackToGenericCounterparts()
    {
        var candidates = Candidates("Eva Klein");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.SINGLE_PERSON }, candidates), Is.SameAs(BflEffectiveSimulation.GENERIC));
            Assert.That(BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.PRESS_CONFERENCE, People = ["Frank Ott"] }, candidates), Is.SameAs(BflEffectiveSimulation.GENERIC));
        }
    }

    [Test]
    public void TheDossiersAreCountedWithTheCharactersPerTokenOfTheMaterial()
    {
        var person = new BflPersonResearch { Name = "Eva Klein" };
        person.Set(BflPersonResearchStage.PROFILE, new BflResearch { Text = new string('a', 4_000), CreatedAtUtc = CREATED_AT_UTC });
        person.Set(BflPersonResearchStage.POSITIONS, new BflResearch { Text = $"  {new string('b', 3_999)}\n", CreatedAtUtc = CREATED_AT_UTC });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflSimulationBudget.DossierTokens([person]), Is.EqualTo(2_000));
            Assert.That(BflSimulationBudget.RequiredTokens([person]), Is.EqualTo(BflModelRequirements.MINIMUM_CONTEXT_TOKENS + 2_000));
        }
    }

    [Test]
    public void AKnownWindowWhichIsTooSmallRefusesAndAnUnknownOneWarns()
    {
        var person = new BflPersonResearch { Name = "Eva Klein" };
        person.Set(BflPersonResearchStage.POSITIONS, new BflResearch { Text = new string('a', 40_000), CreatedAtUtc = CREATED_AT_UTC });
        var simulation = new BflEffectiveSimulation(BflSimulationMode.SINGLE_PERSON, [person]);
        var required = BflSimulationBudget.RequiredTokens([person]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflSimulationBudget.Check(simulation, ContextWindow.Of(required - 1)), Is.EqualTo(BflSimulationFit.TOO_SMALL));
            Assert.That(BflSimulationBudget.Check(simulation, ContextWindow.Of(required)), Is.EqualTo(BflSimulationFit.FITS));
            Assert.That(BflSimulationBudget.Check(simulation, ContextWindow.UNKNOWN), Is.EqualTo(BflSimulationFit.UNKNOWN_CONTEXT));
            Assert.That(BflSimulationBudget.Check(BflEffectiveSimulation.GENERIC, ContextWindow.Of(8_192)), Is.EqualTo(BflSimulationFit.FITS), "Generic counterparts need no room for dossiers.");
            Assert.That(BflSimulationBudget.Check(BflEffectiveSimulation.GENERIC, ContextWindow.UNKNOWN), Is.EqualTo(BflSimulationFit.FITS));
        }
    }

    private static List<BflPersonResearch> Candidates(params string[] names)
    {
        var forms = Forms();
        return [.. BflSimulationCandidates.Of(forms, Topics(forms, names), [.. names.Select(name => Person(forms, name, BflPersonResearchStage.All))])];
    }

    private static BflPersonResearch Person(IReadOnlyDictionary<BflStep, BflStepFormData> forms, string name, IEnumerable<BflPersonResearchStage> stages, string? positionsText = null)
    {
        var person = new BflPersonResearch { Name = name };
        foreach (var stage in stages)
            person.Set(stage, BflResearch.Of(stage is BflPersonResearchStage.POSITIONS && positionsText is not null ? positionsText : $"Text {stage}", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC));

        return person;
    }

    private static Dictionary<BflResearchTopic, BflResearch> Topics(IReadOnlyDictionary<BflStep, BflStepFormData> forms, params string[] names)
    {
        var list = BflResearch.Of(string.Join("\n\n", names.Select(name => $"### {name}")), [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC);
        list.Participants = [.. BflParticipant.ListIn(list.Text)];
        return new() { [BflResearchTopic.PARTICIPANTS] = list };
    }

    private static Dictionary<BflStep, BflStepFormData> Forms()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.FORMAT, "Podium");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_TOPIC, "Klimapolitik");
        return forms;
    }
}