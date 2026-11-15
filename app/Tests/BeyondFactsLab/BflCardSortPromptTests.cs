using System.Globalization;

using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the card sort of the BeyondFacts Lab reaches the chat of its step: as part of the details, with every
/// value of the deck, and never in the chats of the other steps.
/// </summary>
[TestFixture]
public sealed class BflCardSortPromptTests
{
    private const string CORE = "{{DETAILS}}";

    private static readonly BflPromptParts PARTS = new(CORE, "Leitfaden", "Schritt", "Anrede", string.Empty);

    [Test]
    public async Task TheCardSortReachesOnlyTheChatOfItsOwnStep()
    {
        var deck = await EnglishDeckAsync();
        var values = new BflStepFormData { CardSort = new() };
        values.CardSort.Sort("honesty", BflValueCategory.ESPECIALLY_IMPORTANT);
        values.Set(BflFieldCatalog.VALUES_AND_CONCERNS, "Offenheit");
        var forms = new Dictionary<BflStep, BflStepFormData> { [BflStep.VALUES_AND_PERSPECTIVES] = values };

        foreach (var step in BflStep.All)
        {
            var prompt = BflPromptComposer.Compose(PARTS, step, forms, [], "Deutsch", valueDeck: deck);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(prompt.Contains("Werte-Sortierung", StringComparison.Ordinal), Is.EqualTo(step is BflStep.VALUES_AND_PERSPECTIVES), step.ToString());
                Assert.That(prompt, Does.Contain("Offenheit"), "The other details of the step reach every chat, as before.");
            }
        }
    }

    [Test]
    public async Task TheChatSeesEveryValueWithItsCategoryAndTheSource()
    {
        var deck = await EnglishDeckAsync();
        var values = new BflStepFormData { CardSort = new() };
        values.CardSort.Sort("honesty", BflValueCategory.ESPECIALLY_IMPORTANT);
        values.CardSort.Sort("progress", BflValueCategory.LESS_IMPORTANT);

        var prompt = Compose(values, deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("- Werte-Sortierung nach Miller, W. R., C’de Baca, J., Matthews, D. B., & Wilbourne, P. L. (2001). Personal Values Card Sort. University of New Mexico:"));
            Assert.That(prompt, Does.Contain("\n  - Runde 1:\n    - Honesty (Present statements sincerely): Especially important\n"));
            Assert.That(prompt, Does.Contain($"    - Safety (Avoid harm): {BflValueCategory.NONE.Title}\n"));
            Assert.That(prompt, Does.Contain($"    - Progress (Develop new things and make improvements possible): {BflValueCategory.LESS_IMPORTANT.Title}\n"));
            Assert.That(prompt, Does.Contain("\n  - Runde 3:"));
            foreach (var card in deck.Cards)
                Assert.That(prompt, Does.Contain($"    - {card.Name} ({card.Definition}): "), card.Id);
        }
    }

    [Test]
    public async Task TheChatSeesTheOwnValuesAndTheValuesToLookAtMoreClosely()
    {
        var deck = await EnglishDeckAsync();
        var values = new BflStepFormData { CardSort = new() };
        var curiosity = values.CardSort.AddOwnValue("Curiosity", "Ask questions")!;
        values.CardSort.AddOwnValue("Patience", string.Empty);
        values.CardSort.ChooseFocus(["honesty", curiosity.Id]);

        var prompt = Compose(values, deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("  - Eigene Werte der Person:\n    - Curiosity (Ask questions)\n    - Patience (ohne Erläuterung)\n"));
            Assert.That(prompt, Does.Contain("  - Genauer betrachten möchte die Person: Honesty, Curiosity"));
        }
    }

    [Test]
    public async Task WithoutAnySortingTheChatStillSeesTheDeckToOfferIt()
    {
        var deck = await EnglishDeckAsync();

        var prompt = Compose(new BflStepFormData(), deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain($"    - Honesty (Present statements sincerely): {BflValueCategory.NONE.Title}"));
            Assert.That(prompt, Does.Contain("  - Eigene Werte der Person: keine"));
            Assert.That(prompt, Does.Contain("  - Genauer betrachten möchte die Person: noch keine Auswahl"));
        }
    }

    [Test]
    public async Task AValueTheDeckDoesNotKnowStaysOut()
    {
        var deck = await EnglishDeckAsync();
        var values = new BflStepFormData { CardSort = new() { Focus = ["value_of_another_deck"] } };
        values.CardSort.Sort("value_of_another_deck", BflValueCategory.ESPECIALLY_IMPORTANT);

        var prompt = Compose(values, deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Not.Contain("value_of_another_deck"));
            Assert.That(prompt, Does.Contain("noch keine Auswahl"));
        }
    }

    [Test]
    public void WithoutADeckThePromptLeavesTheCardSortOut()
    {
        var values = new BflStepFormData { CardSort = new() };
        values.CardSort.Sort("honesty", BflValueCategory.ESPECIALLY_IMPORTANT);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.VALUES_AND_PERSPECTIVES, new Dictionary<BflStep, BflStepFormData> { [BflStep.VALUES_AND_PERSPECTIVES] = values }, [], "Deutsch");

        Assert.That(prompt, Does.Not.Contain("Werte-Sortierung"));
    }

    [Test]
    public async Task ThePromptOfTheStepLeavesTheValuesToTheDeck()
    {
        var step = await BflPromptLibrary.ReadBuiltInAsync(BflPromptLibrary.StepPath(BflStep.VALUES_AND_PERSPECTIVES));
        var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("de"));

        using (Assert.EnterMultipleScope())
        {
            foreach (var card in deck.Cards)
                Assert.That(step, Does.Not.Contain(card.Definition).IgnoreCase, $"The prompt of the step repeats the value '{card.Id}' of the deck.");

            Assert.That(step, Does.Contain("Werte-Sortierung"));
        }
    }

    [Test]
    public async Task TheRealPromptOfTheStepCarriesTheCardSort()
    {
        var deck = await BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("de"));
        var parts = await BflPromptLibrary.LoadPartsAsync(BflStep.VALUES_AND_PERSPECTIVES, AddressForm.FORMAL);
        var values = new BflStepFormData { CardSort = new() };
        values.CardSort.Sort("honesty", BflValueCategory.ESPECIALLY_IMPORTANT);

        var prompt = BflPromptComposer.Compose(parts, BflStep.VALUES_AND_PERSPECTIVES, new Dictionary<BflStep, BflStepFormData> { [BflStep.VALUES_AND_PERSPECTIVES] = values }, [], "Deutsch", valueDeck: deck);

        Assert.That(prompt, Does.Contain("    - Ehrlichkeit (Aussagen aufrichtig darstellen): "));
    }

    private static string Compose(BflStepFormData values, BflValueDeck deck) => BflPromptComposer.Compose(PARTS, BflStep.VALUES_AND_PERSPECTIVES, new Dictionary<BflStep, BflStepFormData> { [BflStep.VALUES_AND_PERSPECTIVES] = values }, [], "Deutsch", valueDeck: deck);

    private static Task<BflValueDeck> EnglishDeckAsync() => BflValueDeck.LoadAsync(CultureInfo.GetCultureInfo("en"));
}