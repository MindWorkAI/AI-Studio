using System.Globalization;

using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab puts the reflection folder together, without a model.
/// </summary>
/// <remarks>
/// The folder repeats what the person confirmed word for word, and it is meant to be passed on: personal disclosures
/// stay out unless the person includes them.
/// </remarks>
[TestFixture]
public sealed class BflReflectionFolderTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void AConfirmedWorkStateAppearsWordForWordUnderItsStep()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.KNOWLEDGE, new() { ["ESTABLISHED"] = "Gesichert-4711\n\n- mit Liste" });
        var established = BflWorkStateSections.ForStep(BflStep.KNOWLEDGE).Single(section => section.Key == "ESTABLISHED");

        var folder = Build(workStates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(folder, Does.Contain($"## {BflStep.KNOWLEDGE.Title}\n"));
            Assert.That(folder, Does.Contain($"### {established.Title}\n\nGesichert-4711\n\n- mit Liste\n"));
            Assert.That(folder.IndexOf("Gesichert-4711", StringComparison.Ordinal), Is.GreaterThan(folder.IndexOf($"## {BflStep.KNOWLEDGE.Title}", StringComparison.Ordinal)));
        }
    }

    [Test]
    public void ADraftNeverAppears()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>
        {
            [BflStep.KNOWLEDGE] = new() { Draft = new(StringComparer.Ordinal) { ["ESTABLISHED"] = "ENTWURF-4711" } },
        };

        Assert.That(Build(workStates), Does.Not.Contain("ENTWURF-4711"));
    }

    [Test]
    public void EveryStepAppearsInItsOrderAlsoWithoutAConfirmation()
    {
        var folder = Build(new Dictionary<BflStep, BflWorkState>());
        var positions = BflStep.All.Select(step => folder.IndexOf($"## {step.Title}\n", StringComparison.Ordinal)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(positions, Has.All.GreaterThanOrEqualTo(0));
            Assert.That(positions, Is.Ordered);
        }
    }

    [Test]
    public void APersonalSectionStaysOutUnlessThePersonIncludesIt()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.VALUES_AND_PERSPECTIVES, new() { ["VALUES"] = "Gerechtigkeit", ["PERSONAL_INVOLVEMENT"] = "PERSÖNLICH-4711" });

        var without = Build(workStates);
        var with = Build(workStates, includePersonalDisclosures: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(without, Does.Not.Contain("PERSÖNLICH-4711"));
            Assert.That(without, Does.Contain("Gerechtigkeit"));
            Assert.That(with, Does.Contain("PERSÖNLICH-4711"));
        }
    }

    [Test]
    public void TheOpenPointsOfAllStepsAreGatheredAfterTheSteps()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK, new() { ["FORMAT"] = "Podium", ["OPEN_POINTS"] = "OFFEN-1" });
        Confirm(workStates, BflStep.STRATEGY, new() { ["OPEN_POINTS"] = "OFFEN-5" });

        var folder = Build(workStates);
        var lastStep = folder.IndexOf($"## {BflStep.RESULTS.Title}\n", StringComparison.Ordinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(folder.IndexOf("OFFEN-1", StringComparison.Ordinal), Is.GreaterThan(lastStep));
            Assert.That(folder.IndexOf("OFFEN-5", StringComparison.Ordinal), Is.GreaterThan(folder.IndexOf("OFFEN-1", StringComparison.Ordinal)));
            Assert.That(folder.IndexOf("OFFEN-1", StringComparison.Ordinal), Is.EqualTo(folder.LastIndexOf("OFFEN-1", StringComparison.Ordinal)), "The open points appear once.");
        }
    }

    [Test]
    public void TheMaterialIsListedByName()
    {
        var materials = new List<BflMaterial>
        {
            new() { Name = "Studie_2026.pdf", Kind = BflMaterialKind.DOCUMENT, AddedAtUtc = NOW },
            new() { Name = "Interview.m4a", Kind = BflMaterialKind.RECORDING, AddedAtUtc = NOW.AddMinutes(1) },
        };

        var folder = BflReflectionFolder.Build(new BflReflectionFolderInput("Panel", new Dictionary<BflStep, BflWorkState>(), Forms(), materials, new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>(), false, CultureInfo.InvariantCulture));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(folder, Does.Contain("- Studie\\_2026\\.pdf ("));
            Assert.That(folder, Does.Contain("- Interview\\.m4a ("));
        }
    }

    [Test]
    public void TheConversationsAppearOnlyWhenThePersonAddsThem()
    {
        var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>
        {
            [BflStep.STRATEGY] = [new(true, "FRAGE-4711"), new(false, "ANTWORT-4711")],
        };

        var without = Build(new Dictionary<BflStep, BflWorkState>());
        var with = Build(new Dictionary<BflStep, BflWorkState>(), conversations: conversations);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(without, Does.Not.Contain("FRAGE-4711"));
            Assert.That(with, Does.Contain("FRAGE-4711"));
            Assert.That(with.IndexOf("ANTWORT-4711", StringComparison.Ordinal), Is.GreaterThan(with.IndexOf("FRAGE-4711", StringComparison.Ordinal)));
        }
    }

    [Test]
    public void TheConversationOfAStepWithPersonalDisclosuresStaysOutUnlessThePersonIncludesThem()
    {
        var conversations = new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>
        {
            [BflStep.VALUES_AND_PERSPECTIVES] = [new(true, "BETROFFEN-4711")],
            [BflStep.STRATEGY] = [new(true, "STRATEGIE-4711")],
        };

        var without = Build(new Dictionary<BflStep, BflWorkState>(), conversations: conversations);
        var with = Build(new Dictionary<BflStep, BflWorkState>(), includePersonalDisclosures: true, conversations: conversations);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflReflectionFolder.MayHoldPersonalDisclosures(BflStep.VALUES_AND_PERSPECTIVES), Is.True);
            Assert.That(BflReflectionFolder.MayHoldPersonalDisclosures(BflStep.STRATEGY), Is.False);
            Assert.That(without, Does.Not.Contain("BETROFFEN-4711"));
            Assert.That(without, Does.Contain("STRATEGIE-4711"));
            Assert.That(with, Does.Contain("BETROFFEN-4711"));
        }
    }

    [Test]
    public void TheMessagesOfAConversationLeaveOutWhatThePersonNeverSaw()
    {
        var thread = BflThreadFactory.Create(new BflStepThreadLocation(Guid.NewGuid(), BflStep.STRATEGY));
        thread.Blocks.Add(Block(ChatRole.USER, "VERSTECKT-4711", hidden: true));
        thread.Blocks.Add(Block(ChatRole.AI, "Willkommen"));
        thread.Blocks.Add(Block(ChatRole.USER, "  Meine Frage  "));
        thread.Blocks.Add(Block(ChatRole.AI, " "));

        var messages = BflReflectionFolder.MessagesOf(thread);

        Assert.That(messages, Is.EqualTo(new[] { new BflConversationMessage(false, "Willkommen"), new BflConversationMessage(true, "Meine Frage") }));
    }

    [Test]
    public void TheNameOfThePreparationCannotBreakTheHeading()
    {
        var folder = BflReflectionFolder.Build(new BflReflectionFolderInput("Panel\n# Kopf", new Dictionary<BflStep, BflWorkState>(), Forms(), [], new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>(), false, CultureInfo.InvariantCulture));

        Assert.That(folder.Split('\n')[0], Does.StartWith("# ").And.Contain("Panel").And.Contain("Kopf"));
    }

    private static string Build(IReadOnlyDictionary<BflStep, BflWorkState> workStates, bool includePersonalDisclosures = false, IReadOnlyDictionary<BflStep, IReadOnlyList<BflConversationMessage>>? conversations = null) => BflReflectionFolder.Build(new BflReflectionFolderInput("Panel", workStates, Forms(), [], conversations ?? new Dictionary<BflStep, IReadOnlyList<BflConversationMessage>>(), includePersonalDisclosures, CultureInfo.InvariantCulture));

    private static Dictionary<BflStep, BflStepFormData> Forms() => BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());

    private static ContentBlock Block(ChatRole role, string text, bool hidden = false) => new()
    {
        Time = NOW,
        ContentType = ContentType.TEXT,
        Role = role,
        Content = new ContentText { Text = text },
        HideFromUser = hidden,
    };

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