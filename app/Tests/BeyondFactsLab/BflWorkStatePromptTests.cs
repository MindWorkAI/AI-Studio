using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the confirmed work states of the BeyondFacts Lab reach the prompts of the steps.
/// </summary>
/// <remarks>
/// Only what the person confirmed reaches a prompt: a draft never does, since the person has not checked it yet.
/// </remarks>
[TestFixture]
public sealed class BflWorkStatePromptTests
{
    private static readonly BflPromptParts PARTS = new("{{WORK_STATES}}", "Leitfaden", "Schritt", "Anrede");

    private static readonly DateTimeOffset NOW = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ADraftNeverReachesThePrompt()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>
        {
            [BflStep.KNOWLEDGE] = new() { Draft = new(StringComparer.Ordinal) { ["ESTABLISHED"] = "ENTWURF-4711" } },
        };

        var prompt = Compose(BflStep.STRATEGY, workStates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Not.Contain("ENTWURF-4711"));
            Assert.That(prompt, Is.EqualTo("Die Person hat noch keinen Arbeitsstand bestätigt."));
        }
    }

    [Test]
    public void AConfirmedWorkStateReachesThePromptsOfOtherStepsWithItsSections()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.KNOWLEDGE, new() { ["ESTABLISHED"] = "BESTÄTIGT-4711", ["UNCERTAINTIES"] = " " });

        var prompt = Compose(BflStep.STRATEGY, workStates);
        var established = BflWorkStateSections.ForStep(BflStep.KNOWLEDGE).Single(section => section.Key == "ESTABLISHED");
        var uncertainties = BflWorkStateSections.ForStep(BflStep.KNOWLEDGE).Single(section => section.Key == "UNCERTAINTIES");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain($"## {BflStep.KNOWLEDGE.Title}\n### {established.Title}\nBESTÄTIGT-4711"));
            Assert.That(prompt, Does.Not.Contain(uncertainties.Title), "An empty section is left out.");
        }
    }

    [Test]
    public void TheWorkStateOfTheOwnStepIsMarked()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.GOALS_AND_ROLE, new() { ["GOAL"] = "Informieren" });

        var prompt = Compose(BflStep.GOALS_AND_ROLE, workStates);

        Assert.That(prompt, Does.StartWith($"## {BflStep.GOALS_AND_ROLE.Title} (dieser Schritt)"));
    }

    [Test]
    public void APersonalSectionReachesOnlyTheChatOfItsOwnStep()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.VALUES_AND_PERSPECTIVES, new() { ["VALUES"] = "Gerechtigkeit", ["PERSONAL_INVOLVEMENT"] = "PERSÖNLICH-4711" });

        var own = Compose(BflStep.VALUES_AND_PERSPECTIVES, workStates);
        var other = Compose(BflStep.STRATEGY, workStates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(own, Does.Contain("PERSÖNLICH-4711"));
            Assert.That(own, Does.Contain("(persönliche Angabe)"));
            Assert.That(other, Does.Not.Contain("PERSÖNLICH-4711"));
            Assert.That(other, Does.Contain("Gerechtigkeit"));
        }
    }

    [Test]
    public void AWorkStateWithOnlyAPersonalSectionIsLeftOutOfOtherSteps()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.VALUES_AND_PERSPECTIVES, new() { ["PERSONAL_INVOLVEMENT"] = "PERSÖNLICH-4711" });

        Assert.That(Compose(BflStep.STRATEGY, workStates), Is.EqualTo("Die Person hat noch keinen Arbeitsstand bestätigt."));
    }

    [Test]
    public void AnOutdatedWorkStateIsMarked()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.GOALS_AND_ROLE, new() { ["GOAL"] = "Informieren" });
        Confirm(workStates, BflStep.FRAMEWORK, new() { ["FORMAT"] = "Podium" });

        var prompt = Compose(BflStep.STRATEGY, workStates);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain($"## {BflStep.GOALS_AND_ROLE.Title} (veraltet)"));
            Assert.That(prompt, Does.Contain($"## {BflStep.FRAMEWORK.Title}\n"));
        }
    }

    [Test]
    public void ASectionThisVersionDoesNotKnowIsLeftOut()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.GOALS_AND_ROLE, new() { ["GOAL"] = "Informieren", ["FUTURE_SECTION"] = "ZUKUNFT-4711" });

        Assert.That(Compose(BflStep.STRATEGY, workStates), Does.Not.Contain("ZUKUNFT-4711"));
    }

    [Test]
    public async Task TheBuiltInPromptOfEveryStepCarriesTheConfirmedWorkStates()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        Confirm(workStates, BflStep.FRAMEWORK, new() { ["FORMAT"] = "BESTÄTIGT-4711" });

        foreach (var step in BflStep.All)
        {
            var parts = await BflPromptLibrary.LoadPartsAsync(step, AddressForm.FORMAL);
            var prompt = BflPromptComposer.Compose(parts, step, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch", workStates);

            Assert.That(prompt, Does.Contain("BESTÄTIGT-4711"), step.ToString());
        }
    }

    private static string Compose(BflStep step, IReadOnlyDictionary<BflStep, BflWorkState> workStates) => BflPromptComposer.Compose(PARTS, step, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch", workStates);

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