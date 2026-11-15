using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what the agents of the research on a person of the BeyondFacts Lab receive: the public details about the
/// event, the entry of the person in the list of participants, what earlier stages found, and nothing else of a
/// preparation.
/// </summary>
[TestFixture]
public sealed class BflPersonResearchPromptTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly BflParticipant PARTICIPANT = new() { Name = "Eva Klein", Entry = "- Rolle: Podium\n- Institution: TUM" };

    [Test]
    public async Task OnlyPublicContentReachesTheResearchOnAPerson()
    {
        var forms = FormsWithAMarkerInEveryField();
        var details = BflPromptComposer.PublicDetailsOf(forms);
        foreach (var stage in BflPersonResearchStage.All)
        {
            var prompt = BflPromptComposer.ComposePersonResearch(await BflPromptLibrary.LoadPersonResearchPartsAsync(stage), details, PARTICIPANT, [], "Deutsch");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prompt, Does.Contain("Name: Eva Klein"), stage.ToString());
                Assert.That(prompt, Does.Contain("- Rolle: Podium\n- Institution: TUM"), stage.ToString());
                foreach (var field in TextFields())
                {
                    if (field.IsSharedWithResearch)
                        Assert.That(prompt, Does.Contain(Marker(field)), $"{stage}: {field.Id}");
                    else
                        Assert.That(prompt, Does.Not.Contain(Marker(field)), $"{stage}: {field.Id}");
                }
            }
        }
    }

    [Test]
    public void TheResearchOnAPersonHasNoPlaceForAnythingButPublicContent()
    {
        var placeholders = BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.PERSON_RESEARCH_PATH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placeholders, Is.EquivalentTo(new[] { BflPromptTemplate.RESPONSE_LANGUAGE, BflPromptTemplate.TOPIC, BflPromptTemplate.TOOL_CALLS, BflPromptTemplate.PERSON, BflPromptTemplate.EARLIER_STAGES, BflPromptTemplate.PUBLIC_DETAILS }));
            foreach (var stage in BflPersonResearchStage.All)
                Assert.That(BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.PersonResearchStagePath(stage)), Is.Empty, stage.ToString());
        }
    }

    [Test]
    public async Task EveryStageHasATaskOfItsOwn()
    {
        var paths = BflPersonResearchStage.All.Select(BflPromptLibrary.PersonResearchStagePath).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Unique);
            Assert.That(paths.Intersect(BflResearchTopic.All.Select(BflPromptLibrary.ResearchTopicPath)), Is.Empty);
            Assert.That(BflPromptLibrary.AllPaths, Is.SupersetOf(paths).And.Contains(BflPromptLibrary.PERSON_RESEARCH_PATH));
            foreach (var path in paths)
                Assert.That(await BflPromptLibrary.ReadAsync(path), Is.Not.Empty, path);
        }
    }

    [Test]
    public async Task ALaterStageLearnsWhatTheEarlierStagesFound()
    {
        var profile = new BflPersonResearch { Name = PARTICIPANT.Name };
        profile.Set(BflPersonResearchStage.PROFILE, BflResearch.Of("## Person\n\nProfessorin für Klimaökonomie", [], [], [], CREATED_AT_UTC));

        var first = BflPromptComposer.ComposePersonResearch(await BflPromptLibrary.LoadPersonResearchPartsAsync(BflPersonResearchStage.PROFILE), [], PARTICIPANT, [], "Deutsch");
        var second = BflPromptComposer.ComposePersonResearch(await BflPromptLibrary.LoadPersonResearchPartsAsync(BflPersonResearchStage.POSITIONS), [], PARTICIPANT, profile.Stages, "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Does.Contain("Du bist die erste Stufe; es gibt noch keine Ergebnisse."));
            Assert.That(second, Does.Contain($"<stufe titel=\"{BflPersonResearchStage.PROFILE.Title}\" stand=\"2026-11-15\">\n## Person\n\nProfessorin für Klimaökonomie\n</stufe>"));
            Assert.That(second, Does.Not.Contain("Du bist die erste Stufe"));
        }
    }

    [Test]
    public void APersonWithoutAnEntryIsKnownByTheNameOnly()
    {
        var prompt = BflPromptComposer.ComposePersonResearch(new("{{PERSON}}", "Auftrag"), [], new BflParticipant { Name = "Frank Ott" }, [], "Deutsch");

        Assert.That(prompt, Is.EqualTo("Name: Frank Ott\n\nDie Liste nennt nur den Namen."));
    }

    [Test]
    public void ADetailOfAFieldWhichIsNotPublicIsLeftOut()
    {
        BflResearchDetail[] details =
        [
            new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" },
            new() { FieldId = BflFieldCatalog.CONTRIBUTION_TOPIC, Value = "Unveröffentlichte Studie" },
        ];

        var prompt = BflPromptComposer.ComposePersonResearch(new("{{PUBLIC_DETAILS}}", "Auftrag"), details, PARTICIPANT, [], "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("Klimapolitik"));
            Assert.That(prompt, Does.Not.Contain("Unveröffentlichte Studie"));
        }
    }

    [Test]
    public async Task TheAgentOfAStageKnowsItsBudgetOfToolCalls()
    {
        var prompt = BflPromptComposer.ComposePersonResearch(await BflPromptLibrary.LoadPersonResearchPartsAsync(BflPersonResearchStage.APPEARANCES), [], PARTICIPANT, [], "Deutsch");

        Assert.That(prompt, Does.Contain($"höchstens {ToolSelectionRules.MAX_TOOL_CALLS} Werkzeugaufrufe"));
    }

    [Test]
    public async Task TheRequestNamesSectionsOfThePromptOfThePeople()
    {
        var prompt = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.PERSON_RESEARCH_PATH)).ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            foreach (var section in new[] { "Dein Auftrag", "Vorgehen", "Form" })
            {
                Assert.That(BflResearchRunner.REQUEST, Does.Contain($"„{section}“"));
                Assert.That(prompt, Does.Contain($"\n# {section}\n"));
            }
        }
    }

    [Test]
    public void AStageKeepsLessTextThanATopic()
    {
        var answer = string.Join("\n", Enumerable.Repeat(new string('x', 99), 200));

        var topic = BflResearch.Of(answer, [], [], [], CREATED_AT_UTC);
        var stage = BflResearch.Of(answer, [], [], [], CREATED_AT_UTC, BflPersonResearch.MAX_STAGE_TEXT_CHARACTERS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stage.Text.Length, Is.LessThanOrEqualTo(BflPersonResearch.MAX_STAGE_TEXT_CHARACTERS));
            Assert.That(stage.Text.Length, Is.LessThan(topic.Text.Length));
            Assert.That(stage.Text, Does.EndWith("x"));
        }
    }

    private static IEnumerable<BflFieldDefinition> TextFields() => BflFieldCatalog.FIELDS.Where(field => field.Kind is not (BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE));

    private static string Marker(BflFieldDefinition field) => $"MARKER-{field.Id}";

    private static Dictionary<BflStep, BflStepFormData> FormsWithAMarkerInEveryField()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        foreach (var field in TextFields())
            forms[field.Step].Set(field.Id, Marker(field));

        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.Kind is BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE))
            forms[field.Step].SetChoices(field, [field.Options[0].Id]);

        return forms;
    }
}