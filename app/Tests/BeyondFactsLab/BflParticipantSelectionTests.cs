using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab selects whom of the list of participants to research in depth: the suggestion of the
/// model, which the lab checks strictly, and the selection which counts for a list.
/// </summary>
[TestFixture]
public sealed class BflParticipantSelectionTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly BflParticipant[] PARTICIPANTS =
    [
        new() { Name = "Eva Klein", Entry = "- Rolle: Podium" },
        new() { Name = "Frank Ott", Entry = "- Rolle: Moderation" },
        new() { Name = "Ida Lux", Entry = "- Rolle: Keynote" },
    ];

    [Test]
    public void ASuggestionWithNamesFromTheListIsAcceptedInTheSpellingOfTheList()
    {
        var check = BflParticipantSelectionCheck.Read("Hier ist meine Auswahl:\n```json\n{ \"participants\": [ \"frank  OTT\", \"Eva Klein\" ] }\n```", PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Problems, Is.Empty);
            Assert.That(check.Names, Is.EqualTo(new[] { "Frank Ott", "Eva Klein" }));
        }
    }

    [Test]
    public void ASuggestionWithANameWhichIsNotOnTheListIsRejectedAsAWhole()
    {
        var check = BflParticipantSelectionCheck.Read("""{ "participants": [ "Eva Klein", "Anna Erfunden" ] }""", PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Names, Is.Empty);
            Assert.That(check.Problems, Is.EqualTo(new[] { "„Anna Erfunden“ steht nicht in der Liste der Teilnehmenden." }));
        }
    }

    [Test]
    public void ASuggestionWithANameTwiceIsRejected()
    {
        var check = BflParticipantSelectionCheck.Read("""{ "participants": [ "Eva Klein", "eva klein" ] }""", PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Names, Is.Empty);
            Assert.That(check.Problems, Is.EqualTo(new[] { "„Eva Klein“ steht mehr als einmal in der Auswahl." }));
        }
    }

    [Test]
    public void ASuggestionWithTooManyNamesIsRejected()
    {
        var participants = Enumerable.Range(1, BflParticipantSelection.MAX_SUGGESTED_PEOPLE + 1).Select(number => new BflParticipant { Name = $"Person {number}" }).ToList();
        var answer = $$"""{ "participants": [ {{string.Join(", ", participants.Select(participant => $"\"{participant.Name}\""))}} ] }""";

        var check = BflParticipantSelectionCheck.Read(answer, participants);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Names, Is.Empty);
            Assert.That(check.Problems.Single(), Does.Contain($"höchstens {BflParticipantSelection.MAX_SUGGESTED_PEOPLE}"));
        }
    }

    [TestCase("""{ "participants": [] }""", "Die Auswahl ist leer; sie braucht mindestens einen Namen.")]
    [TestCase("""{ "participants": [ "  " ] }""", "Ein Eintrag der Auswahl ist leer.")]
    [TestCase("""{ "people": [ "Eva Klein" ] }""", "Das Feld „participants“ fehlt.")]
    [TestCase("Eva Klein und Frank Ott", "Die Antwort enthält kein vollständiges JSON-Objekt.")]
    public void AnEmptyOrIncompleteSuggestionIsRejected(string answer, string problem)
    {
        var check = BflParticipantSelectionCheck.Read(answer, PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Names, Is.Empty);
            Assert.That(check.Problems, Does.Contain(problem));
        }
    }

    [Test]
    public void ASuggestionWhichNamesTheFieldTwiceIsRejected()
    {
        var check = BflParticipantSelectionCheck.Read("""{ "participants": [ "Eva Klein" ], "participants": [ "Frank Ott" ] }""", PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Names, Is.Empty);
            Assert.That(check.Problems.Single(), Does.StartWith("Das JSON-Objekt ist nicht lesbar"));
        }
    }

    [Test]
    public void AShortListCountsAsSelectedAsAWholeUntilSomebodyChooses()
    {
        var list = List(PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflParticipantSelection.SelectedIn(list, null).Select(participant => participant.Name), Is.EqualTo(PARTICIPANTS.Select(participant => participant.Name)));
            Assert.That(BflParticipantSelection.NeedsSuggestion(list, null), Is.False);
        }
    }

    [Test]
    public void ALongListCountsAsNotSelectedAndNeedsASuggestion()
    {
        var list = List([.. Enumerable.Range(1, BflParticipantSelection.MAX_SUGGESTED_PEOPLE + 1).Select(number => new BflParticipant { Name = $"Person {number}" })]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflParticipantSelection.SelectedIn(list, null), Is.Empty);
            Assert.That(BflParticipantSelection.NeedsSuggestion(list, null), Is.True);
        }
    }

    [Test]
    public void ASelectionCountsForItsListInTheOrderOfTheList()
    {
        var list = List(PARTICIPANTS);
        var selection = new BflParticipantSelection { Names = ["ida lux", "Eva Klein", "Nicht Gelistet"], ListCreatedAtUtc = CREATED_AT_UTC };

        Assert.That(BflParticipantSelection.SelectedIn(list, selection).Select(participant => participant.Name), Is.EqualTo(new[] { "Eva Klein", "Ida Lux" }));
    }

    [Test]
    public void ASelectionForAnEarlierListNoLongerCounts()
    {
        var list = List([.. Enumerable.Range(1, BflParticipantSelection.MAX_SUGGESTED_PEOPLE + 1).Select(number => new BflParticipant { Name = $"Person {number}" })]);
        var earlier = new BflParticipantSelection { Names = ["Person 1"], ListCreatedAtUtc = CREATED_AT_UTC.AddDays(-1) };
        var current = new BflParticipantSelection { Names = ["Person 1"], ListCreatedAtUtc = CREATED_AT_UTC };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflParticipantSelection.SelectedIn(list, earlier), Is.Empty);
            Assert.That(BflParticipantSelection.NeedsSuggestion(list, earlier), Is.True);
            Assert.That(BflParticipantSelection.SelectedIn(list, current).Single().Name, Is.EqualTo("Person 1"));
            Assert.That(BflParticipantSelection.NeedsSuggestion(list, current), Is.False);
        }
    }

    [Test]
    public async Task TheSuggestionKnowsTheFirstThreeStepsAndTheListButNothingConfided()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.Kind is not (BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE)))
            forms[field.Step].Set(field.Id, $"MARKER-{field.Id}");

        var template = await BflPromptLibrary.ReadAsync(BflPromptLibrary.PARTICIPANT_SELECTION_PATH);
        var prompt = BflPromptComposer.ComposeParticipantSelection(template, forms, PARTICIPANTS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("### Eva Klein\n- Rolle: Podium"));
            Assert.That(prompt, Does.Contain($"höchstens {BflParticipantSelection.MAX_SUGGESTED_PEOPLE} Personen"));
            Assert.That(prompt, Does.Contain($"MARKER-{BflFieldCatalog.CONTRIBUTION_TOPIC}"));
            foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.Kind is not (BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE)))
            {
                if (field.Step >= BflStep.VALUES_AND_PERSPECTIVES || field.IsPersonalDisclosure)
                    Assert.That(prompt, Does.Not.Contain($"MARKER-{field.Id}"), field.Id);
            }
        }
    }

    [Test]
    public void TheSuggestionHasNoPlaceForMoreThanTheDetailsAndTheList()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.PARTICIPANT_SELECTION_PATH), Is.EquivalentTo(new[] { BflPromptTemplate.SELECTION_SIZE, BflPromptTemplate.DETAILS, BflPromptTemplate.PARTICIPANTS }));
            Assert.That(BflPromptLibrary.AllPaths, Does.Contain(BflPromptLibrary.PARTICIPANT_SELECTION_PATH));
        }
    }

    [Test]
    public async Task TheRequestNamesSectionsOfThePromptOfTheSelection()
    {
        var prompt = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.PARTICIPANT_SELECTION_PATH)).ReplaceLineEndings("\n");
        var request = BflResearchRunner.SELECTION_REQUEST;

        using (Assert.EnterMultipleScope())
        {
            foreach (var section in new[] { "Vorgehen", "Form" })
            {
                Assert.That(request, Does.Contain($"„{section}“"));
                Assert.That(prompt, Does.Contain($"\n# {section}\n"));
            }
        }
    }

    [Test]
    public void TheRepairRequestNamesEveryProblem()
    {
        var request = BflPromptComposer.ComposeParticipantSelectionRepairRequest(["Erstes Problem.", "Zweites Problem."]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Contain("- Erstes Problem.\n- Zweites Problem."));
            Assert.That(request, Does.Contain("nur mit Namen aus der Liste"));
        }
    }

    [Test]
    public void AChatWithoutToolsGetsNoTool()
    {
        var provider = new AIStudio.Settings.Provider(1, "provider-1", "Test", AIStudio.Provider.LLMProviders.SELF_HOSTED, new AIStudio.Provider.Model("model", null));
        var hidden = BflThreadFactory.CreateHidden("participant-selection");

        BflThreadFactory.PrepareForRequest(hidden, provider, "Prompt");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden.RuntimeSelectedToolIds, Is.Empty);
            Assert.That(hidden.RuntimeSelectedToolIds, Does.Not.Contain(ToolSelectionRules.WEB_SEARCH_TOOL_ID));
            Assert.That(hidden.RuntimeToolsAreAssistantManaged, Is.True);
        }
    }

    private static BflResearch List(IReadOnlyList<BflParticipant> participants) => new() { Participants = [.. participants], CreatedAtUtc = CREATED_AT_UTC };
}