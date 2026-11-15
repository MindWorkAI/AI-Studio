using System.Globalization;

using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the value deck of the BeyondFacts Lab: that the app comes with it in every language it names, that every
/// language has the same values in the same rounds, and that a deck with a wrong form is refused.
/// </summary>
/// <remarks>
/// The tests run a development build, which reads the decks from its output folder: the project file copies every
/// embedded file there as well.
/// </remarks>
[TestFixture]
public sealed class BflValueDeckTests
{
    private static IEnumerable<string> Languages() => BflValueDeck.LANGUAGES.Order(StringComparer.Ordinal);

    [TestCaseSource(nameof(Languages))]
    public async Task EveryLanguageHasAValidDeck(string language)
    {
        var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo(language));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck.Problems(), Is.Empty);
            Assert.That(deck.Cards, Has.Count.EqualTo(BflValueDeck.ROUNDS * BflValueDeck.VALUES_PER_ROUND));
            Assert.That(deck.Citation, Does.Contain("Miller").And.Contain("2001").And.Contain("Personal Values Card Sort"));
        }
    }

    [Test]
    public async Task EveryLanguageHasTheSameValuesInTheSameRounds()
    {
        var english = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("en"));
        foreach (var language in Languages())
        {
            var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo(language));
            Assert.That(deck.Rounds.Select(round => round.Select(card => card.Id)), Is.EqualTo(english.Rounds.Select(round => round.Select(card => card.Id))), language);
        }
    }

    [TestCase("de-DE", "values.de.json")]
    [TestCase("de-AT", "values.de.json")]
    [TestCase("en-US", "values.en.json")]
    [TestCase("fr-FR", "values.en.json")]
    [TestCase("", "values.en.json")]
    public void TheAppShowsTheDeckOfItsLanguageOrElseTheEnglishOne(string culture, string fileName)
    {
        Assert.That(BflValueDeck.PathOf(CultureInfo.GetCultureInfo(culture)), Does.EndWith($"/{fileName}"));
    }

    [Test]
    public async Task AValueIsFoundByItsId()
    {
        var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("en"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deck.Find("honesty")?.Name, Is.EqualTo("Honesty"));
            Assert.That(deck.Find("unknown"), Is.Null);
        }
    }

    [Test]
    public void ADeckWithAWrongNumberOfRoundsOrValuesIsRefused()
    {
        var deck = ValidDeck();
        deck.Rounds.RemoveAt(2);
        deck.Rounds[0].RemoveAt(3);

        Assert.That(deck.Problems(), Has.Count.EqualTo(2));
    }

    [Test]
    public void ADeckWithAnIdTwiceOrAValueWithoutItsTextsIsRefused()
    {
        var deck = ValidDeck();
        deck.Rounds[1][0].Id = deck.Rounds[0][0].Id;
        deck.Rounds[2][0].Definition = " ";

        Assert.That(deck.Problems(), Has.Count.EqualTo(2));
    }

    [Test]
    public void ADeckWithoutItsSourceIsRefused()
    {
        var deck = ValidDeck();
        deck.Citation = string.Empty;

        Assert.That(deck.Problems(), Has.One.Contains("source"));
    }

    private static BflValueDeck ValidDeck() => new()
    {
        Citation = "Miller et al. (2001)",
        Rounds = [.. Enumerable.Range(0, BflValueDeck.ROUNDS).Select(round => Enumerable.Range(0, BflValueDeck.VALUES_PER_ROUND).Select(index => new BflValueCard { Id = $"value_{round}_{index}", Name = "Name", Definition = "Definition" }).ToList())],
    };
}