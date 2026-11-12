using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which names the BeyondFacts Lab suggests to remove before the person shares a session.
/// </summary>
[TestFixture]
public sealed class BflShareNamesTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheNamesInTheFieldOfTheOtherParticipantsAreSuggested()
    {
        var forms = Forms();
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.OTHER_PARTICIPANTS, "Prof. Dr. Anna Schmidt (TUM)\nModeration: Bernd Weber; Carla von der Heide und Dieter Graf, Bundestag\n- Die Moderatorin Hanna Berg\nVertreter*innen aus der Politik");

        var names = BflShareNames.Suggest(forms, new Dictionary<BflStep, BflWorkState>(), null);
        Assert.That(names, Is.EqualTo(new[] { "Anna Schmidt", "Bernd Weber", "Carla von der Heide", "Dieter Graf", "Bundestag", "Hanna Berg" }));
    }

    [Test]
    public void TheHeadingsOfTheResearchOnTheParticipantsAreSuggested()
    {
        var research = new BflResearch
        {
            Text = "## Teilnehmende\n\n### Dr. Eva Klein\n\n- Rolle: Podium\n\n### Frank Ott (Moderation)\n\n## Offene Punkte\n\n- Wer noch?",
            CreatedAtUtc = NOW,
        };

        var names = BflShareNames.Suggest(Forms(), new Dictionary<BflStep, BflWorkState>(), research);
        Assert.That(names, Is.EqualTo(new[] { "Eva Klein", "Frank Ott" }));
    }

    [Test]
    public void TheNamesInBoldInTheSectionOnTheParticipantsAreSuggested()
    {
        var workStates = new Dictionary<BflStep, BflWorkState> { [BflStep.STRATEGY] = new() };
        workStates[BflStep.STRATEGY].Confirm(new Dictionary<string, string> { ["PARTICIPANTS"] = "- **Gregor Sand**, Moderation, hält das für **wichtig**\n- [**Ida Lux**](https://example.org)" }, new Dictionary<BflStep, int>(), string.Empty, NOW);

        var names = BflShareNames.Suggest(Forms(), workStates, null);
        Assert.That(names, Is.EqualTo(new[] { "Gregor Sand", "Ida Lux" }));
    }

    [Test]
    public void ANameFoundTwiceOrAsAPartOfALongerOneIsSuggestedOnce()
    {
        var forms = Forms();
        forms[BflStep.FRAMEWORK].Set(BflFieldCatalog.OTHER_PARTICIPANTS, "Weber\nBernd Weber\nbernd weber");

        var names = BflShareNames.Suggest(forms, new Dictionary<BflStep, BflWorkState>(), null);
        Assert.That(names, Is.EqualTo(new[] { "Bernd Weber" }));
    }

    [Test]
    public void ALongRunOfCapitalsGivesItsLastTwoWords() => Assert.That(BflShareNames.NamesIn("Neue Forschung Zeigt Lena Graf"), Is.EqualTo(new[] { "Lena Graf" }));

    [Test]
    public void ADraftOfAWorkStateSuggestsNothing()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>
        {
            [BflStep.STRATEGY] = new() { Draft = new(StringComparer.Ordinal) { ["PARTICIPANTS"] = "**Gregor Sand**" } },
        };

        Assert.That(BflShareNames.Suggest(Forms(), workStates, null), Is.Empty);
    }

    private static Dictionary<BflStep, BflStepFormData> Forms() => BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
}