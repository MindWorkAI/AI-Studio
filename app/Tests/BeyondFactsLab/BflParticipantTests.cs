using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab reads the list of people from the research on the participants.
/// </summary>
[TestFixture]
public sealed class BflParticipantTests
{
    [Test]
    public void EveryHeadingOfTheThirdLevelIsAPersonWithTheTextBelowIt()
    {
        const string TEXT = "## Programm\n\n[Programm](https://example.org/programm)\n\n## Teilnehmende\n\n### Eva Klein\n\n- Rolle: Podium\n- Quelle: [Programm](https://example.org/programm)\n\n### Frank Ott\n\n- Rolle: Moderation\n\n## Offene Punkte\n\n- Wer noch?";

        var participants = BflParticipant.ListIn(TEXT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participants.Select(participant => participant.Name), Is.EqualTo(new[] { "Eva Klein", "Frank Ott" }));
            Assert.That(participants[0].Entry, Is.EqualTo("- Rolle: Podium\n- Quelle: [Programm](https://example.org/programm)"));
            Assert.That(participants[1].Entry, Is.EqualTo("- Rolle: Moderation"));
        }
    }

    [Test]
    public void AHeadingOfALowerLevelBelongsToTheEntry()
    {
        var participants = BflParticipant.ListIn("### Eva Klein\n\n#### Rolle\n\nPodium\n# Ende\n\nNicht mehr dazu");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participants.Single().Name, Is.EqualTo("Eva Klein"));
            Assert.That(participants.Single().Entry, Is.EqualTo("#### Rolle\n\nPodium"));
        }
    }

    [Test]
    public void MarkdownInAHeadingIsTakenOff()
    {
        var participants = BflParticipant.ListIn("### **Eva   Klein**\n### [Frank Ott](https://example.org/ott) ###\n### `Ida Lux`");

        Assert.That(participants.Select(participant => participant.Name), Is.EqualTo(new[] { "Eva Klein", "Frank Ott", "Ida Lux" }));
    }

    [Test]
    public void EmptyAndOverlongHeadingsAreNoPeople()
    {
        var participants = BflParticipant.ListIn($"### \n### **  **\n### {new string('A', BflParticipant.MAX_NAME_CHARACTERS + 1)}\n### Eva Klein");

        Assert.That(participants.Select(participant => participant.Name), Is.EqualTo(new[] { "Eva Klein" }));
    }

    [Test]
    public void ANameWhichComesTwiceCountsOnceWithItsFirstEntry()
    {
        var participants = BflParticipant.ListIn("### Eva Klein\n\nPodium\n\n### eva  KLEIN\n\nModeration");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participants.Single().Name, Is.EqualTo("Eva Klein"));
            Assert.That(participants.Single().Entry, Is.EqualTo("Podium"));
        }
    }

    [Test]
    public void AListHoldsAtMostTheLargestNumberOfPeople()
    {
        var text = string.Join("\n", Enumerable.Range(1, BflParticipant.MAX_PARTICIPANTS + 5).Select(number => $"### Person {number}\n\nEintrag {number}"));

        var participants = BflParticipant.ListIn(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participants, Has.Count.EqualTo(BflParticipant.MAX_PARTICIPANTS));
            Assert.That(participants[^1].Entry, Is.EqualTo($"Eintrag {BflParticipant.MAX_PARTICIPANTS}"));
        }
    }

    [Test]
    public void LinesEndingInCarriageReturnAndLineFeedAreRead()
    {
        var participants = BflParticipant.ListIn("### Eva Klein\r\n\r\nPodium\r\n### Frank Ott");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participants.Select(participant => participant.Name), Is.EqualTo(new[] { "Eva Klein", "Frank Ott" }));
            Assert.That(participants[0].Entry, Is.EqualTo("Podium"));
        }
    }

    [Test]
    public void AnAnswerWithoutHeadingsListsNobody()
    {
        Assert.That(BflParticipant.ListIn("Die Recherche fand keine Teilnehmenden."), Is.Empty);
    }

    [Test]
    public void ANameIsTheSameRegardlessOfCaseAndSpaces()
    {
        var participant = new BflParticipant { Name = "Eva Klein" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(participant.HasName(" eva   KLEIN "), Is.True);
            Assert.That(participant.HasName("Eva Kleine"), Is.False);
        }
    }
}