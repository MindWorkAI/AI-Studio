using System.Globalization;

using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the card sort of the BeyondFacts Lab: values stay open until the person sorts them, values of their own keep
/// their limits, and at most three values get looked at more closely.
/// </summary>
[TestFixture]
public sealed class BflCardSortTests
{
    [Test]
    public void AValueStaysOpenUntilThePersonSortsIt()
    {
        var cardSort = new BflCardSort();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardSort.CategoryOf("honesty"), Is.EqualTo(BflValueCategory.NONE));
            Assert.That(cardSort.IsEmpty, Is.True);
        }
    }

    [Test]
    public void ASortedValueCanBeOpenedAgain()
    {
        var cardSort = new BflCardSort();
        cardSort.Sort("honesty", BflValueCategory.ESPECIALLY_IMPORTANT);
        cardSort.Sort("honesty", BflValueCategory.UNCLEAR);
        var changed = cardSort.CategoryOf("honesty");

        cardSort.Sort("honesty", BflValueCategory.NONE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(BflValueCategory.UNCLEAR));
            Assert.That(cardSort.Categories, Is.Empty);
            Assert.That(cardSort.IsEmpty, Is.True);
        }
    }

    [Test]
    public void TheButtonsOfferEveryCategoryButNone()
    {
        Assert.That(BflValueCategory.All, Is.EquivalentTo(Enum.GetValues<BflValueCategory>().Where(category => category is not BflValueCategory.NONE)));
    }

    [Test]
    public void AnOwnValueNeedsAName()
    {
        var cardSort = new BflCardSort();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardSort.AddOwnValue("  ", "Meaning"), Is.Null);
            Assert.That(cardSort.OwnValues, Is.Empty);
        }
    }

    [Test]
    public void AnOwnValueIsTrimmedAndCut()
    {
        var cardSort = new BflCardSort();

        var value = cardSort.AddOwnValue($"  {new string('n', BflCardSort.MAX_NAME_LENGTH + 5)}  ", new string('d', BflCardSort.MAX_DEFINITION_LENGTH + 5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(value!.Name, Has.Length.EqualTo(BflCardSort.MAX_NAME_LENGTH));
            Assert.That(value.Definition, Has.Length.EqualTo(BflCardSort.MAX_DEFINITION_LENGTH));
            Assert.That(value.Id, Is.Not.Empty);
            Assert.That(cardSort.IsEmpty, Is.False);
        }
    }

    [Test]
    public void APersonAddsAtMostTenValuesOfTheirOwn()
    {
        var cardSort = new BflCardSort();
        for (var index = 0; index < BflCardSort.MAX_OWN_VALUES; index++)
            cardSort.AddOwnValue($"Value {index}", string.Empty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardSort.CanAddOwnValue, Is.False);
            Assert.That(cardSort.AddOwnValue("One more", string.Empty), Is.Null);
            Assert.That(cardSort.OwnValues, Has.Count.EqualTo(BflCardSort.MAX_OWN_VALUES));
        }
    }

    [Test]
    public void AnOwnValueKeepsItsNameWhenTheNewOneIsEmpty()
    {
        var cardSort = new BflCardSort();
        var value = cardSort.AddOwnValue("Curiosity", "Ask questions")!;

        var emptied = cardSort.ChangeOwnValue(value.Id, " ", "Something else");
        var changed = cardSort.ChangeOwnValue(value.Id, "Courage", " Speak up ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(emptied, Is.False);
            Assert.That(changed, Is.True);
            Assert.That(value.Name, Is.EqualTo("Courage"));
            Assert.That(value.Definition, Is.EqualTo("Speak up"));
            Assert.That(cardSort.ChangeOwnValue("unknown", "Name", string.Empty), Is.False);
        }
    }

    [Test]
    public void ARemovedOwnValueLeavesTheValuesToLookAtMoreClosely()
    {
        var cardSort = new BflCardSort();
        var value = cardSort.AddOwnValue("Curiosity", string.Empty)!;
        cardSort.ChooseFocus(["honesty", value.Id]);

        cardSort.RemoveOwnValue(value.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardSort.OwnValues, Is.Empty);
            Assert.That(cardSort.Focus, Is.EqualTo(new[] { "honesty" }));
        }
    }

    [Test]
    public void AtMostThreeValuesAreLookedAtMoreClosely()
    {
        var cardSort = new BflCardSort();
        cardSort.ChooseFocus(["honesty", "openness"]);

        cardSort.ChooseFocus(["honesty", "openness", "respect", "care"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cardSort.Focus, Has.Count.EqualTo(BflCardSort.MAX_FOCUS));
            Assert.That(cardSort.Focus.Take(2), Is.EqualTo(new[] { "honesty", "openness" }));
        }
    }

    [Test]
    public void ValuesChosenEarlierKeepTheirPlace()
    {
        var cardSort = new BflCardSort();
        cardSort.ChooseFocus(["openness"]);

        cardSort.ChooseFocus(["honesty", "openness"]);

        Assert.That(cardSort.Focus, Is.EqualTo(new[] { "openness", "honesty" }));
    }

    [Test]
    public async Task OnlyValuesWhichExistCountAsLookedAtMoreClosely()
    {
        var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("en"));
        var cardSort = new BflCardSort();
        var value = cardSort.AddOwnValue("Curiosity", string.Empty)!;
        cardSort.Focus = ["value_of_another_deck", "honesty", "honesty", value.Id, "openness", "respect"];

        var focus = cardSort.FocusIn(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(focus, Is.EqualTo(new[] { "honesty", value.Id, "openness" }));
            Assert.That(cardSort.NameOf(value.Id, deck), Is.EqualTo("Curiosity"));
            Assert.That(cardSort.NameOf("honesty", deck), Is.EqualTo("Honesty"));
            Assert.That(cardSort.NameOf("value_of_another_deck", deck), Is.Null);
        }
    }
}