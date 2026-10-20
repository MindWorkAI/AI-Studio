using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks which slide decks are sent back to the model, and what it is told.
/// </summary>
/// <remarks>
/// Uses the default catalog: L1 title slide, L2 section header, L3 title and content, L4 two
/// content, L5 content with picture, and L6 table. Template slides need a template with slides,
/// which <see cref="TemplateWithSlides"/> builds.
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

    [Test]
    public void TemplateSlidesKeptAndEditedInTheirOrderHaveNoIssues()
    {
        var deck = new SlideDeckSpec("Weekly",
        [
            Keep(1),
            Slide("L2", "Agenda", texts: [Text("News", "Questions")]),
            Edit(2, "News", texts: [Text("AI Studio: a new release", "EINSTEIN: a new model")]),
            Keep(3),
            Edit(4, texts: [Text("Thema:\tWeekly", "Datum:\t2026-10-19")]),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 4, USES), Is.Empty, "The imprint of the template closes the presentation without counting as a slide of it.");
            Assert.That(SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 5, USES), Has.Some.Contains("has 4 slides, but exactly 5 were asked for"));
        });
    }

    [Test]
    public void EveryChosenTemplateSlideAppearsExactlyOnce()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Keep(1), Keep(5), Edit(4, texts: [Text("Thema:\tWeekly")])]);

        var issues = SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 0, USES);
        Assert.Multiple(() =>
        {
            Assert.That(issues, Has.Some.Contains("Slide 3 refers to template slide 5, which is not offered. Refer only to template slides 1, 2, 3, 4."));
            Assert.That(issues, Has.Some.Contains("Template slide 1 appears 2 times."));
            Assert.That(issues, Has.Some.Contains("Template slide 2 is missing. Add it at its place with \"template_slide\": 2 and its new content."));
            Assert.That(issues, Has.Some.Contains("Template slide 3 is missing. Add it at its place as { \"template_slide\": 3 }."));
        });
    }

    [Test]
    public void AKeptSlideTakesNoContentAndAnEditedOneNeedsContentWhichFits()
    {
        var deck = new SlideDeckSpec("Weekly",
        [
            Edit(1, "A new title"),
            Keep(2),
            Edit(3, "Questions", subtitle: "Ask us"),
            Edit(4, texts: [Text("Thema:\tWeekly")]),
        ]);

        var issues = SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 0, USES);
        Assert.Multiple(() =>
        {
            Assert.That(issues, Has.Some.Contains("Slide 1 keeps template slide 1 unchanged, so it takes no content."));
            Assert.That(issues, Has.Some.Contains("Slide 2 is to edit template slide 2, but writes no content."));
            Assert.That(issues, Has.Some.Contains("Slide 3 keeps template slide 3 unchanged"), "Slide 3 of the template is to be kept, so the edit is sent back.");
        });

        var editedDeck = new SlideDeckSpec("Weekly", [Keep(1), Edit(2, "News", subtitle: "This week"), Keep(3), Edit(4, texts: [Text("Thema:\tWeekly")])]);
        Assert.That(SlideDeckSpecValidator.Validate(editedDeck, TemplateWithSlides(), 0, 0, USES), Has.Some.Contains("Slide 2 has a subtitle, but layout L2 has none"), "An edited slide has the slots of its template slide.");
    }

    [Test]
    public void TheTemplateSlidesKeepTheirOrderAndTheImprintClosesThePresentation()
    {
        var unordered = new SlideDeckSpec("Weekly", [Keep(3), Edit(2, "News", texts: [Text("New")]), Keep(1), Edit(4, texts: [Text("Thema:\tWeekly")])]);
        var imprintInBetween = new SlideDeckSpec("Weekly", [Keep(1), Edit(2, "News", texts: [Text("New")]), Keep(3), Edit(4, texts: [Text("Thema:\tWeekly")]), Slide("L2", "Thanks")]);

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.Validate(unordered, TemplateWithSlides(), 0, 0, USES), Has.Some.Contains("The template slides are out of order. Keep them in the order 1, 2, 3, 4"));
            Assert.That(SlideDeckSpecValidator.Validate(imprintInBetween, TemplateWithSlides(), 0, 0, USES), Has.Some.Contains("Slide 4 is the imprint of the template, which closes the presentation. Move it to the end."));
        });
    }

    [Test]
    public void OnlySlidesOnKnownLayoutsAndChosenTemplateSlidesCanBeWritten()
    {
        var template = TemplateWithSlides();
        TemplateSlideUse[] uses = [TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.OMIT, TemplateSlideUse.KEEP, TemplateSlideUse.KEEP];

        Assert.Multiple(() =>
        {
            Assert.That(SlideDeckSpecValidator.CanWrite(Slide("L2"), template, uses), Is.True);
            Assert.That(SlideDeckSpecValidator.CanWrite(Slide("L9"), template, uses), Is.False);
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(5), template, uses), Is.True, "A slide on a layout without room for content can be kept.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Edit(5, "Thanks"), template, uses), Is.False, "Such a slide has no slots to write content into.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(3), template, uses), Is.False, "The user left this slide out.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(9), template, uses), Is.False);
        });
    }

    [Test]
    public void AnEditedSlideFitsTheSizeOfItsSlotOnTheSlide()
    {
        // The layout slot holds 10 lines of 50 characters; the slide halves it to 5 lines of 25:
        var bounds = new SlotBounds(838_200, 1_825_625, 6_413_500 + 2 * 91_440, 3_200_400 + 2 * 45_720);
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides: [new TestSlide(0, (TestPlaceholder.Title(), "Rocket"), (TestPlaceholder.Text(1, bounds with { Width = bounds.Width / 2, Height = bounds.Height / 2 }), "Reusable"))]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, bounds, 2000)));

        var template = PresentationTemplate.Read(file);
        var points = Text(Enumerable.Repeat(new string('x', 40), 4).ToArray());
        var edited = SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [Edit(1, texts: [points])]), template, 0, 0, [TemplateSlideUse.EDIT]);
        var written = SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [Slide("L1", texts: [points])]), template, 0, 0, [TemplateSlideUse.OMIT]);

        Assert.Multiple(() =>
        {
            Assert.That(edited, Has.Some.Contains("Slide 1: text 1 needs about 8 lines, but only about 5 fit."), "Four points of two lines each overfill the half the slide leaves.");
            Assert.That(written, Is.Empty, "On a slide written anew, the same points fit the whole slot.");
        });
    }

    [Test]
    public void AnEditedSlideRemovesOnlyWhatItsTemplateSlideHas()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "News"), (TestPlaceholder.Label(17, new SlotBounds(838_200, 1_400_000, 3_000_000, 300_000)), "Lorem ipsum"), (new TestPlaceholder(null, 1), "Old news"))
                {
                    Elements = [TestElement.TextBox(new SlotBounds(6_200_000, 5_400_000, 5_150_000, 300_000), "Bildquelle hier angeben")],
                },
                new TestSlide(0, (TestPlaceholder.Title(), "Questions")),
            ]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Label(17, new SlotBounds(838_200, 1_400_000, 3_000_000, 300_000)), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)));

        var template = PresentationTemplate.Read(file);
        TemplateSlideUse[] uses = [TemplateSlideUse.EDIT, TemplateSlideUse.KEEP];
        IReadOnlyList<string> IssuesOf(SlideSpec first, SlideSpec? added = null) => SlideDeckSpecValidator.Validate(new SlideDeckSpec("Weekly", added is null ? [first, Keep(2)] : [first, Keep(2), added]), template, 0, 0, uses);

        Assert.Multiple(() =>
        {
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 1", "label 1"] }), Is.Empty, "Removing alone edits a slide.");
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 2"] }), Has.Some.Contains("Slide 1 removes \"element 2\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["picture 1"] }), Has.Some.Contains("Slide 1 removes \"picture 1\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["title"] }), Has.Some.Contains("Slide 1 removes the title of template slide 1."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("New news")]) with { Removals = ["text 1"] }), Has.Some.Contains("Slide 1 fills text 1 and removes it as well."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 1"] }, Slide("L1") with { Removals = ["element 1"] }), Has.Some.Contains("Slide 3 is a new slide, which has nothing to remove."));
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Weekly", [Edit(1) with { Removals = ["element 1"] }, Keep(2) with { Removals = ["element 1"] }]), template, 0, 0, uses), Has.Some.Contains("Slide 2 keeps template slide 2 unchanged, so it takes no content."));
        });
    }

    [Test]
    public void AnEditedSlideFillsAndRemovesTheCopiesOfItsSlots()
    {
        using var file = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout());

        var template = PresentationTemplate.Read(file);
        IReadOnlyList<string> IssuesOf(SlideSpec slide) => SlideDeckSpecValidator.Validate(new SlideDeckSpec("Missions", [slide]), template, 0, 0, [TemplateSlideUse.EDIT]);

        Assert.Multiple(() =>
        {
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon"), Text("Venus")], labels: ["Near", "Far", "Farther"])), Is.Empty, "Every copy takes content like a slot.");
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon"), Text("Venus"), Text("Jupiter")])), Has.Some.Contains("Slide 1 has 4 texts, but layout L1 has only 3."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text(new string('x', 120))])), Has.Some.Contains("Slide 1: text 2 needs about 5 lines, but only about 3 fit."), "A copy holds what its size allows.");
            Assert.That(IssuesOf(Edit(1, labels: ["Near", "Far"]) with { Removals = ["label 3", "text 3"] }), Is.Empty);
            Assert.That(IssuesOf(Edit(1, labels: ["Near"]) with { Removals = ["label 4"] }), Has.Some.Contains("Slide 1 removes \"label 4\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon")]) with { Removals = ["text 2"] }), Has.Some.Contains("Slide 1 fills text 2 and removes it as well."));
        });
    }

    /// <summary>
    /// What the user chose for the slides of <see cref="TemplateWithSlides"/>: the imprint is
    /// edited, and the closing slide is left out.
    /// </summary>
    private static readonly TemplateSlideUse[] USES = [TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.OMIT];

    /// <summary>
    /// A template with a title slide, two content slides, an imprint, and a closing slide on a
    /// layout without room for content. Its families are L1 for the title and L2 for the content.
    /// </summary>
    private static PresentationTemplate TemplateWithSlides()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Consultation hour")),
                new TestSlide(1, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:")),
                new TestSlide(1, (TestPlaceholder.Title(), "Questions")),
                new TestSlide(1, (TestPlaceholder.Title(), "Impressum"), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour")),
                new TestSlide(2, (TestPlaceholder.Title(), "Thank you")),
            ]),
            new TestLayout("Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(new SlotBounds(838_200, 3_602_038, 10_515_600, 1_655_762))),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)),
            new TestLayout("Title Only", TestPlaceholder.Title()));

        return PresentationTemplate.Read(file);
    }

    private static SlideSpec Keep(int templateSlide) => new(string.Empty, false, string.Empty, string.Empty, [], [], [], null, templateSlide);

    private static SlideSpec Edit(int templateSlide, string title = "", string subtitle = "", IReadOnlyList<SlideTextBlock>? texts = null, IReadOnlyList<string>? labels = null) => new(string.Empty, false, title, subtitle, texts ?? [], labels ?? [], [], null, templateSlide);

    private static SlideSpec Slide(
        string layout,
        string title = "Title",
        string subtitle = "",
        IReadOnlyList<SlideTextBlock>? texts = null,
        IReadOnlyList<int>? pictures = null,
        SlideTableSpec? table = null) => new(layout, false, title, subtitle, texts ?? [], [], pictures ?? [], table);

    private static SlideTextBlock Text(params string[] paragraphs) => new(paragraphs.Select(paragraph => new SlideParagraph(paragraph, 0)).ToList());
}