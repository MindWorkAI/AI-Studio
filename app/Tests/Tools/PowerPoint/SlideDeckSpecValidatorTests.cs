using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks which slide decks are sent back to the model, and what it is told.
/// </summary>
/// <remarks>
/// Uses the default catalog: L1 title slide, L2 section header, L3 title and content, L4 two
/// content, L5 content with picture, and L6 table.
/// </remarks>
[TestFixture]
public sealed class SlideDeckSpecValidatorTests
{
    private static readonly PresentationTemplate TEMPLATE = PresentationTemplate.CreateDefault();

    [Test]
    public void AFittingDeckHasNoIssues()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L1", "Mars", subtitle: "A plan"),
            Slide("L3", "Why Mars", texts: [Text("Close to Earth", "Has water ice", "Has a day of 24.6 hours")]),
            Slide("L5", "The rocket", texts: [Text("Reusable")], pictures: [1]),
            Slide("L6", "Costs", table: new SlideTableSpec(["Item", "Cost"], [["Fuel", "10"], ["Crew", "20"]])),
        ]);

        Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 1, 4), Is.Empty);
    }

    [Test]
    public void AnUnknownLayoutIsNamedWithTheOnesWhichExist()
    {
        var issues = SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [Slide("L9")]), TEMPLATE, 0, 0);

        Assert.That(issues, Has.Some.Contains("uses the layout \"L9\", which does not exist. Use one of L1, L2, L3, L4, L5, L6."));
    }

    [Test]
    public void ContentWithoutASlotIsSentBack()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L3", texts: [Text("One"), Text("Two")]),
            Slide("L3", subtitle: "A subtitle"),
            Slide("L3", pictures: [1]),
            Slide("L3", table: new SlideTableSpec(["Item"], [["Fuel"]])),
        ]);

        var issues = SlideDeckSpecValidator.Validate(deck, TEMPLATE, 1, 0);
        Assert.Multiple(() =>
        {
            Assert.That(issues, Has.Some.Contains("Slide 1 has 2 texts, but layout L3 has only 1"));
            Assert.That(issues, Has.Some.Contains("Slide 2 has a subtitle, but layout L3 has none"));
            Assert.That(issues, Has.Some.Contains("Slide 3 has 1 picture, but layout L3 has no picture"));
            Assert.That(issues, Has.Some.Contains("Slide 4 has a table, but layout L3 has none"));
        });
    }

    [Test]
    public void AnEmptyEntryKeepsItsSlotWithoutTakingOne()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L4", texts: [Text(), Text("Only the right side")]),
            Slide("L3", texts: [Text("One"), Text()]),
        ]);

        Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Is.Empty, "An empty text at the end takes no slot, so one text fits a layout with one.");
    }

    [Test]
    public void TextClearlyBeyondTheSlotIsSentBack()
    {
        // The text slot of L3 holds about 9 lines at 28 pt. A little more is accepted, since the
        // template shrinks text which runs long; much more is not:
        var fitting = Slide("L3", texts: [Text(Enumerable.Range(1, 10).Select(number => $"Point {number}").ToArray())]);
        var tooLong = Slide("L3", texts: [Text(Enumerable.Range(1, 11).Select(number => $"Point {number}").ToArray())]);

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [fitting]), TEMPLATE, 0, 0), Is.Empty);
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [tooLong]), TEMPLATE, 0, 0), Has.Some.Contains("Slide 1: text 1 needs about 11 lines, but only about 9 fit"));
        });
    }

    [Test]
    public void APictureWhichIsNotAttachedIsSentBack()
    {
        var deck = new SlideDeckSpec("Mars", [Slide("L5", texts: [Text("Reusable")], pictures: [2])]);

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 1, 0), Has.Some.Contains("only pictures 1 to 1 are attached"));
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Has.Some.Contains("no pictures are attached"));
        });
    }

    [Test]
    public void ARowWithTheWrongNumberOfCellsIsSentBack()
    {
        var deck = new SlideDeckSpec("Mars", [Slide("L6", table: new SlideTableSpec(["Item", "Cost"], [["Fuel", "10", "per ton"]]))]);

        Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Has.Some.Contains("row 1 of the table has 3 cells, but the table has 2 columns"));
    }

    [Test]
    public void TheNumberOfSlidesTheUserAskedForIsKept()
    {
        var deck = new SlideDeckSpec("Mars", [Slide("L3", texts: [Text("One")])]);

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 3), Has.Some.Contains("exactly 3 were asked for"));
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Is.Empty, "0 leaves the number to the model.");
        });
    }

    private static SlideSpec Slide(
        string layout,
        string title = "Title",
        string subtitle = "",
        IReadOnlyList<SlideTextBlock>? texts = null,
        IReadOnlyList<int>? pictures = null,
        SlideTableSpec? table = null) => new(layout, false, title, subtitle, texts ?? [], [], pictures ?? [], table);

    private static SlideTextBlock Text(params string[] paragraphs) => new(paragraphs.Select(paragraph => new SlideParagraph(paragraph, 0)).ToList());
}