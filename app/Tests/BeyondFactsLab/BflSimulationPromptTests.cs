using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the simulation the person chose reaches the prompt of the step "Test situation" of the BeyondFacts Lab.
/// </summary>
[TestFixture]
public sealed class BflSimulationPromptTests
{
    private const string SIMULATION_TEXT = "Regeln der Simulation";

    private static readonly BflPromptParts PARTS = new("{{STEP}}\n---\n{{RESEARCH}}\n---\n{{DETAILS}}", "Leitfaden", "Schritt", "Anrede", string.Empty, SIMULATION_TEXT);

    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private string dataDirectory = string.Empty;

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-simulation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task TheLibraryReadsTheSimulationOnlyForTheTestSituationWithPeople()
    {
        var rules = await BflPromptLibrary.ReadAsync(BflPromptLibrary.PERSONA_PATH);
        var panel = await BflPromptLibrary.ReadAsync(BflPromptLibrary.SimulationPath(BflSimulationMode.PANEL_DISCUSSION));

        var withPanel = await BflPromptLibrary.LoadPartsAsync(BflStep.TEST_SITUATION, AddressForm.FORMAL, simulation: BflSimulationMode.PANEL_DISCUSSION);
        var generic = await BflPromptLibrary.LoadPartsAsync(BflStep.TEST_SITUATION, AddressForm.FORMAL);
        var otherStep = await BflPromptLibrary.LoadPartsAsync(BflStep.STRATEGY, AddressForm.FORMAL, simulation: BflSimulationMode.PANEL_DISCUSSION);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withPanel.Simulation, Is.EqualTo($"{rules.Trim()}\n\n{panel.Trim()}"));
            Assert.That(generic.Simulation, Is.Empty);
            Assert.That(otherStep.Simulation, Is.Empty);
            Assert.That(BflPromptLibrary.AllPaths, Does.Contain(BflPromptLibrary.PERSONA_PATH));
            Assert.That(BflPromptLibrary.AllPaths, Is.SupersetOf(BflSimulationMode.All.Where(mode => mode.HasPeople).Select(BflPromptLibrary.SimulationPath)));
            Assert.Throws<ArgumentOutOfRangeException>(() => BflPromptLibrary.SimulationPath(BflSimulationMode.GENERIC));
        }
    }

    [Test]
    public void TheTestSituationCarriesTheSimulationAndOnlyTheDossiersOfTheChosenPeople()
    {
        var forms = Forms();
        var candidates = Candidates(forms, ("Eva Klein", "Moderation; Universität Beispielstadt"), ("Frank Ott", "Podiumsgast"));
        var simulation = BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.SINGLE_PERSON, People = ["Eva Klein"] }, candidates);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.TEST_SITUATION, forms, [], "Deutsch", simulation: simulation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.StartWith($"Schritt\n\n{SIMULATION_TEXT}\n---\n"));
            Assert.That(prompt, Does.Contain("<person name=\"Eva Klein\">\n<eintrag>\nModeration; Universität Beispielstadt\n</eintrag>\n"));
            Assert.That(prompt, Does.Contain($"<stufe titel=\"{BflPersonResearchStage.POSITIONS.Title}\" stand=\"2026-11-15\">\nText {BflPersonResearchStage.POSITIONS}\n</stufe>"));
            Assert.That(prompt, Does.Not.Contain("Frank Ott"), "A candidate whom the person did not choose stays out.");
            Assert.That(prompt, Does.Contain($"- Simulation: {BflSimulationMode.SINGLE_PERSON.Title}; gewählt: Eva Klein"));
        }
    }

    [Test]
    public void AnEmptyEntryInTheListSaysSo()
    {
        var forms = Forms();
        var simulation = BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.SINGLE_PERSON, People = ["Eva Klein"] }, Candidates(forms, ("Eva Klein", "  ")));

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.TEST_SITUATION, forms, [], "Deutsch", simulation: simulation);

        Assert.That(prompt, Does.Contain("<eintrag>\nDie Liste nennt nur den Namen.\n</eintrag>"));
    }

    [Test]
    public void GenericCounterpartsCarryNoSimulationAndNoDossier()
    {
        var forms = Forms();
        Candidates(forms, ("Eva Klein", "Podiumsgast"));

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.TEST_SITUATION, forms, [], "Deutsch", simulation: BflEffectiveSimulation.GENERIC);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.StartWith("Schritt\n---\n"));
            Assert.That(prompt, Does.Not.Contain(SIMULATION_TEXT));
            Assert.That(prompt, Does.Contain("Die Recherche nutzt der Schritt „Strategie“."));
            Assert.That(prompt, Does.Not.Contain("- Simulation:"));
        }
    }

    [Test]
    public void EveryOtherStepIgnoresTheSimulation()
    {
        var forms = Forms();
        var simulation = BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.SINGLE_PERSON, People = ["Eva Klein"] }, Candidates(forms, ("Eva Klein", "Podiumsgast")));

        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All.Where(step => step is not BflStep.TEST_SITUATION))
            {
                var prompt = BflPromptComposer.Compose(PARTS, step, forms, [], "Deutsch", simulation: simulation);
                Assert.That(prompt, Does.Not.Contain(SIMULATION_TEXT), step.ToString());
                Assert.That(prompt, Does.Not.Contain("Eva Klein"), step.ToString());
            }
        }
    }

    [Test]
    public async Task ThePromptOfASimulationMarksItAndForbidsMadeUpQuotes()
    {
        var forms = Forms();
        var simulation = BflEffectiveSimulation.Of(new() { Mode = BflSimulationMode.PRESS_CONFERENCE, People = ["Eva Klein"] }, Candidates(forms, ("Eva Klein", "Podiumsgast")));
        var parts = await BflPromptLibrary.LoadPartsAsync(BflStep.TEST_SITUATION, AddressForm.FORMAL, simulation: simulation.Mode);

        var prompt = BflPromptComposer.Compose(parts, BflStep.TEST_SITUATION, forms, [], "Deutsch", simulation: simulation);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("### Simulation recherchierter Personen"));
            Assert.That(prompt, Does.Contain("### Pressekonferenz"));
            Assert.That(prompt, Does.Contain("(Simulation):**"));
            Assert.That(prompt, Does.Contain("(Simulation, keine reale Person):**"));
            Assert.That(prompt, Does.Contain("Erfinde keine Zitate."));
            Assert.That(prompt, Does.Contain("„Pause“"));
            Assert.That(prompt, Does.Contain("<person name=\"Eva Klein\">"));
        }
    }

    [Test]
    public async Task ThePromptOfTheTestSituationTellsHowTheButtonEndsAnExercise()
    {
        var step = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.StepPath(BflStep.TEST_SITUATION))).ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(step, Does.Contain("\n### Übung beenden\n"));
            Assert.That(step, Does.Contain("folge dann „Nach einer Übung“"));
            Assert.That(step, Does.Contain("\n### Nach einer Übung\n"));
        }
    }

    [Test]
    public async Task TheServiceLoadsWhomOnlyTheTestSituationPlays()
    {
        var store = new BeyondFactsLabStore(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
        var service = new BflConversationService(store, null!, new SettingsManager(NullLogger<SettingsManager>.Instance, null!), null!, null!, NullLogger<BflConversationService>.Instance);
        var created = await store.CreateAsync("Panel");
        var forms = Forms();
        await store.SaveFormAsync(created.ProjectId, BflStep.FRAMEWORK, forms[BflStep.FRAMEWORK]);
        await store.SaveFormAsync(created.ProjectId, BflStep.TEST_SITUATION, new BflStepFormData { Simulation = new() { Mode = BflSimulationMode.PANEL_DISCUSSION, People = ["Frank Ott", "Eva Klein"] } });
        await store.SaveResearchAsync(created.ProjectId, BflResearchTopic.PARTICIPANTS, Participants(forms, ("Eva Klein", "Moderation"), ("Frank Ott", "Podiumsgast")));
        await store.SavePersonResearchAsync(created.ProjectId, Person(forms, "Eva Klein"));
        await store.SavePersonResearchAsync(created.ProjectId, Person(forms, "Frank Ott"));

        var storedForms = await store.LoadFormsAsync(created.ProjectId);
        var testSituation = await service.LoadSimulationAsync(new(created.ProjectId, BflStep.TEST_SITUATION), storedForms);
        var strategy = await service.LoadSimulationAsync(new(created.ProjectId, BflStep.STRATEGY), storedForms);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(testSituation.Mode, Is.EqualTo(BflSimulationMode.PANEL_DISCUSSION));
            Assert.That(testSituation.People.Select(person => person.Name), Is.EqualTo(new[] { "Eva Klein", "Frank Ott" }));
            Assert.That(testSituation.People[0].Participant.Entry, Is.EqualTo("Moderation"));
            Assert.That(strategy, Is.SameAs(BflEffectiveSimulation.GENERIC));
        }
    }

    private static List<BflSimulatedPerson> Candidates(IReadOnlyDictionary<BflStep, BflStepFormData> forms, params (string Name, string Entry)[] people)
    {
        var topics = new Dictionary<BflResearchTopic, BflResearch> { [BflResearchTopic.PARTICIPANTS] = Participants(forms, people) };
        return [.. BflSimulationCandidates.Of(forms, topics, [.. people.Select(person => Person(forms, person.Name))])];
    }

    private static BflResearch Participants(IReadOnlyDictionary<BflStep, BflStepFormData> forms, params (string Name, string Entry)[] people)
    {
        var list = BflResearch.Of(string.Join("\n\n", people.Select(person => $"### {person.Name}\n{person.Entry}")), [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC);
        list.Participants = [.. BflParticipant.ListIn(list.Text)];
        return list;
    }

    private static BflPersonResearch Person(IReadOnlyDictionary<BflStep, BflStepFormData> forms, string name)
    {
        var person = new BflPersonResearch { Name = name };
        foreach (var stage in BflPersonResearchStage.All)
            person.Set(stage, BflResearch.Of($"Text {stage}", [], [], BflPromptComposer.PublicDetailsOf(forms), CREATED_AT_UTC));

        return person;
    }

    private static Dictionary<BflStep, BflStepFormData> Forms()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.FORMAT, "Podium");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_TOPIC, "Klimapolitik");
        return forms;
    }
}