using System.Globalization;

using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what a session shared with the research team of the BeyondFacts Lab holds, before anonymizing it.
/// </summary>
/// <remarks>
/// The research team learns how the person worked with the lab. Whatever points to the person or to the event stays
/// out: the name of the preparation, the material, the research, the fields which pin down the event, and personal
/// disclosures unless the person includes them.
/// </remarks>
[TestFixture]
public sealed class BflShareModelBuilderTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheFieldsWhichPinDownTheEventStayOut()
    {
        var forms = Forms();
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.FORMAT, "Podium-4711");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_DATE, "DATUM-4711");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_LOCATION, "ORT-4711");
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_LINK, "https://example.org/LINK-4711");

        var session = Build(forms: forms);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session, Does.Contain("Podium-4711"));
            Assert.That(session, Does.Not.Contain("DATUM-4711"));
            Assert.That(session, Does.Not.Contain("ORT-4711"));
            Assert.That(session, Does.Not.Contain("LINK-4711"));
        }
    }

    [Test]
    public void TheLinkDateAndPlaceOfTheEventAreTheFieldsWhichPinItDown()
    {
        var identifying = BflFieldCatalog.FIELDS.Where(field => field.IdentifiesEvent).Select(field => field.Id);
        string[] expected = [BflFieldCatalog.EVENT_DATE, BflFieldCatalog.EVENT_LOCATION, BflFieldCatalog.EVENT_LINK];
        Assert.That(identifying, Is.EquivalentTo(expected));
    }

    [Test]
    public void TheMaterialAppearsOnlyAsItsCount()
    {
        var materials = new List<BflMaterial>
        {
            new() { MaterialId = Guid.NewGuid(), Name = "Entwurf-Schmidt-4711.pdf", Kind = BflMaterialKind.DOCUMENT, AddedAtUtc = NOW },
            new() { MaterialId = Guid.NewGuid(), Name = "Probe-4711.m4a", Kind = BflMaterialKind.RECORDING, AddedAtUtc = NOW },
            new() { MaterialId = Guid.NewGuid(), Name = "Folie-4711.png", Kind = BflMaterialKind.DOCUMENT, AddedAtUtc = NOW },
        };

        var session = Build(materials: materials);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session, Does.Not.Contain("4711"));
            Assert.That(session, Does.Contain("2 documents, 1 recordings, 0 images"));
        }
    }

    [Test]
    public void PersonalDisclosuresStayOutUnlessThePersonIncludesThem()
    {
        var forms = Forms();
        forms[BflStep.VALUES_AND_PERSPECTIVES].Set(BflFieldCatalog.PERSONAL_CONCERN, "FELD-PERSÖNLICH-4711");
        forms[BflStep.VALUES_AND_PERSPECTIVES].Set(BflFieldCatalog.VALUES_AND_CONCERNS, "Gerechtigkeit");
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.VALUES_AND_PERSPECTIVES, new() { ["VALUES"] = "Offenheit", ["PERSONAL_INVOLVEMENT"] = "STAND-PERSÖNLICH-4711" });
        var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>
        {
            [BflStep.VALUES_AND_PERSPECTIVES] = [new(true, "GESPRÄCH-PERSÖNLICH-4711")],
        };

        var without = Build(forms: forms, workStates: workStates, conversations: conversations);
        var with = Build(forms: forms, workStates: workStates, conversations: conversations, includePersonalDisclosures: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(without, Does.Not.Contain("PERSÖNLICH-4711"));
            Assert.That(without, Does.Contain("Gerechtigkeit"));
            Assert.That(without, Does.Contain("Offenheit"));
            Assert.That(with, Does.Contain("FELD-PERSÖNLICH-4711"));
            Assert.That(with, Does.Contain("STAND-PERSÖNLICH-4711"));
            Assert.That(with, Does.Contain("GESPRÄCH-PERSÖNLICH-4711"));
        }
    }

    [Test]
    public void AConversationOfAStepWithoutPersonalDisclosuresAppearsWithItsSpeakers()
    {
        var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>
        {
            [BflStep.KNOWLEDGE] = [new(true, "Frage-4711"), new(false, "Antwort-4711")],
        };

        var session = Build(conversations: conversations);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session, Does.Contain("**Person:**\n\nFrage-4711\n"));
            Assert.That(session, Does.Contain("**Lab:**\n\nAntwort-4711\n"));
        }
    }

    [Test]
    public void ChoicesAppearWithTheLabelsOfTheirOptions()
    {
        var forms = Forms();
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.EVENT_VISIBILITY, BflFieldCatalog.EVENT_NOT_PUBLIC);
        var goals = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.GOAL_DIRECTIONS);
        forms[BflStep.GOALS_AND_ROLE].SetChoices(goals, [BflFieldCatalog.GOAL_INFORM, BflFieldCatalog.GOAL_MODERATE]);

        var session = Build(forms: forms);
        var visibility = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.EVENT_VISIBILITY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session, Does.Contain(visibility.Options.Single(option => option.Id == BflFieldCatalog.EVENT_NOT_PUBLIC).Label));
            Assert.That(session, Does.Contain($"{goals.Options[0].Label}, {goals.Options[1].Label}"));
            Assert.That(session, Does.Not.Contain(BflFieldCatalog.EVENT_NOT_PUBLIC));
            Assert.That(session, Does.Not.Contain(BflFieldCatalog.GOAL_INFORM));
        }
    }

    [Test]
    public void DraftsAndTheReflectionFolderNeverAppear()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>
        {
            [BflStep.KNOWLEDGE] = new() { Draft = new(StringComparer.Ordinal) { ["ESTABLISHED"] = "ENTWURF-4711" } },
        };

        var results = new Dictionary<BflResultKind, BflResult>
        {
            [BflResultKind.POSITION_PAPER] = new() { Draft = new() { Text = "MATERIAL-ENTWURF-4711" } },
            [BflResultKind.REFLECTION_FOLDER] = new() { Confirmed = new() { Text = "MAPPE-4711" } },
        };

        Assert.That(Build(workStates: workStates, results: results), Does.Not.Contain("4711"));
    }

    [Test]
    public void TheConfirmedMaterialsOfTheLabAppearInTheLastStep()
    {
        var results = new Dictionary<BflResultKind, BflResult>
        {
            [BflResultKind.POSITION_PAPER] = new() { Confirmed = new() { Text = "Position-4711" } },
        };

        var session = Build(results: results);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(session, Does.Contain($"### {BflResultKind.POSITION_PAPER.Title}\n\nPosition-4711\n"));
            Assert.That(session.IndexOf("Position-4711", StringComparison.Ordinal), Is.GreaterThan(session.IndexOf($"## {BflStep.RESULTS.Title}\n", StringComparison.Ordinal)));
        }
    }

    [Test]
    public void TheTopicsOfTheResearchAppearByTheirTitles()
    {
        var session = Build(researchTopics: [BflResearchTopic.DEBATE, BflResearchTopic.EVENT]);
        Assert.That(session, Does.Contain($"{BflResearchTopic.EVENT.Title}, {BflResearchTopic.DEBATE.Title}"));
    }

    [Test]
    public void EveryStepAppearsInItsOrder()
    {
        var session = Build();
        var positions = BflStep.All.Select(step => session.IndexOf($"## {step.Title}\n", StringComparison.Ordinal)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(positions, Has.All.GreaterThanOrEqualTo(0));
            Assert.That(positions, Is.Ordered);
        }
    }

    [Test]
    public void EveryLineEndsWithLf()
    {
        var forms = Forms();
        forms[BflStep.KNOWLEDGE].Set(BflFieldCatalog.CORE_THESES, "Erste\r\nZweite");
        var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>
        {
            [BflStep.KNOWLEDGE] = [new(false, "Eins\r\nZwei")],
        };

        Assert.That(Build(forms: forms, conversations: conversations), Does.Not.Contain("\r"));
    }

    private static string Build(Dictionary<BflStep, BflStepFormData>? forms = null, IReadOnlyDictionary<BflStep, BflWorkState>? workStates = null, IReadOnlyDictionary<BflResultKind, BflResult>? results = null, IReadOnlyDictionary<BflStep, IReadOnlyList<BflConversationMessage>>? conversations = null, IReadOnlyList<BflMaterial>? materials = null, IReadOnlyCollection<BflResearchTopic>? researchTopics = null, bool includePersonalDisclosures = false) => BflShareModelBuilder.Build(new BflShareInput(
        forms ?? Forms(),
        workStates ?? new Dictionary<BflStep, BflWorkState>(),
        results ?? new Dictionary<BflResultKind, BflResult>(),
        conversations ?? new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>(),
        materials ?? [],
        researchTopics ?? [],
        includePersonalDisclosures,
        "26.11.2",
        NOW,
        CultureInfo.InvariantCulture));

    private static Dictionary<BflStep, BflStepFormData> Forms() => BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());

    private static void Confirm(Dictionary<BflStep, BflWorkState> workStates, BflStep step, Dictionary<string, string> sections)
    {
        if (!workStates.TryGetValue(step, out var workState))
        {
            workState = new BflWorkState();
            workStates[step] = workState;
        }

        workState.Confirm(sections, BflStaleness.UpstreamRevisionsOf(step, workStates), BflStaleness.FingerprintOf(new BflStepFormData()), NOW);
    }
}