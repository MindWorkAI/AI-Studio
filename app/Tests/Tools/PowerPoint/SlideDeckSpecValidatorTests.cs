using AIStudio.Tools.PowerPoint;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks which slide decks are sent back to the model, and what it is told.
/// </summary>
/// <remarks>
/// Uses the default catalog: L1 title slide, L2 section header, L3 title and content, L4 two
/// content, L5 content with picture, and L6 table. Template slides need a template with slides,
/// which TemplateWithSlides builds.
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Slide 1 has 2 texts, but layout L3 has only 1"));
            Assert.That(issues, Has.Some.Contains("Slide 2 has a subtitle, but layout L3 has none"));
            Assert.That(issues, Has.Some.Contains("Slide 3 has 1 picture, but layout L3 has no picture"));
            Assert.That(issues, Has.Some.Contains("Slide 4 has a table, but layout L3 has none"));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [fitting]), TEMPLATE, 0, 0), Is.Empty);
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [tooLong]), TEMPLATE, 0, 0), Has.Some.Contains("Slide 1: text 1 needs about 11 lines, but only about 9 fit"));
        }
    }

    [Test]
    public void APictureWhichIsNotAttachedIsSentBack()
    {
        var deck = new SlideDeckSpec("Mars", [Slide("L5", texts: [Text("Reusable")], pictures: [2])]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 1, 0), Has.Some.Contains("only pictures 1 to 1 are attached"));
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Has.Some.Contains("no pictures are attached"));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 3), Has.Some.Contains("exactly 3 were asked for"));
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Is.Empty, "0 leaves the number to the model.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 5, USES), Is.Empty, "Every template slide counts as a slide of the presentation, an imprint included.");
            Assert.That(SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 4, USES), Has.Some.Contains("has 5 slides, but exactly 4 were asked for"));
        }
    }

    [Test]
    public void EveryChosenTemplateSlideAppearsExactlyOnce()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Keep(1), Keep(5), Edit(4, texts: [Text("Thema:\tWeekly")])]);

        var issues = SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 0, 0, USES);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Slide 3 refers to template slide 5, which is not offered. Refer only to template slides 1, 2, 3, 4."));
            Assert.That(issues, Has.Some.Contains("Template slide 1 appears 2 times."));
            Assert.That(issues, Has.Some.Contains("Template slide 2 is missing. Add it at its place with \"template_slide\": 2 and its new content."));
            Assert.That(issues, Has.Some.Contains("Template slide 3 is missing. Add it at its place as { \"template_slide\": 3 }."));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Slide 1 keeps template slide 1 unchanged, so it takes no content."));
            Assert.That(issues, Has.Some.Contains("Slide 2 is to edit template slide 2, but writes no content."));
            Assert.That(issues, Has.Some.Contains("Slide 3 keeps template slide 3 unchanged"), "Slide 3 of the template is to be kept, so the edit is sent back.");
        }

        var editedDeck = new SlideDeckSpec("Weekly", [Keep(1), Edit(2, "News", subtitle: "This week"), Keep(3), Edit(4, texts: [Text("Thema:\tWeekly")])]);
        Assert.That(SlideDeckSpecValidator.Validate(editedDeck, TemplateWithSlides(), 0, 0, USES), Has.Some.Contains("Slide 2 has a subtitle, but layout L2 has none"), "An edited slide has the slots of its template slide.");
    }

    [Test]
    public void TheTemplateSlidesKeepTheirOrder()
    {
        var unordered = new SlideDeckSpec("Weekly", [Keep(3), Edit(2, "News", texts: [Text("New")]), Keep(1), Edit(4, texts: [Text("Thema:\tWeekly")])]);
        var imprintInBetween = new SlideDeckSpec("Weekly", [Keep(1), Edit(2, "News", texts: [Text("New")]), Keep(3), Edit(4, texts: [Text("Thema:\tWeekly")]), Slide("L2", "Thanks")]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(unordered, TemplateWithSlides(), 0, 0, USES), Has.Some.Contains("The template slides are out of order. Keep them in the order 1, 2, 3, 4"));
            Assert.That(SlideDeckSpecValidator.Validate(imprintInBetween, TemplateWithSlides(), 0, 0, USES), Is.Empty, "An imprint is a template slide like any other, so a new slide may follow it.");
        }
    }

    [Test]
    public void OnlySlidesOnKnownLayoutsAndChosenTemplateSlidesCanBeWritten()
    {
        var template = TemplateWithSlides();
        TemplateSlideUse[] uses = [TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.OMIT, TemplateSlideUse.KEEP, TemplateSlideUse.KEEP];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.CanWrite(Slide("L2"), template, uses), Is.True);
            Assert.That(SlideDeckSpecValidator.CanWrite(Slide("L9"), template, uses), Is.False);
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(5), template, uses), Is.True, "A slide on a layout without room for content can be kept.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Edit(5, "Thanks"), template, uses), Is.False, "Such a slide has no slots to write content into.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(3), template, uses), Is.False, "The user left this slide out.");
            Assert.That(SlideDeckSpecValidator.CanWrite(Keep(9), template, uses), Is.False);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited, Has.Some.Contains("Slide 1: text 1 needs about 8 lines, but only about 5 fit."), "Four points of two lines each overfill the half the slide leaves.");
            Assert.That(written, Is.Empty, "On a slide written anew, the same points fit the whole slot.");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 1", "label 1"] }), Is.Empty, "Removing alone edits a slide.");
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 2"] }), Has.Some.Contains("Slide 1 removes \"element 2\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["picture 1"] }), Has.Some.Contains("Slide 1 removes \"picture 1\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["title"] }), Has.Some.Contains("Slide 1 removes the title of template slide 1."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("New news")]) with { Removals = ["text 1"] }), Has.Some.Contains("Slide 1 fills text 1 and removes it as well."));
            Assert.That(IssuesOf(Edit(1) with { Removals = ["element 1"] }, Slide("L1") with { Removals = ["element 1"] }), Has.Some.Contains("Slide 3 is a new slide, which has nothing to remove."));
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Weekly", [Edit(1) with { Removals = ["element 1"] }, Keep(2) with { Removals = ["element 1"] }]), template, 0, 0, uses), Has.Some.Contains("Slide 2 keeps template slide 2 unchanged, so it takes no content."));
        }
    }

    [Test]
    public void AnEditedSlideFillsAndRemovesTheCopiesOfItsSlots()
    {
        using var file = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout());

        var template = PresentationTemplate.Read(file);
        IReadOnlyList<string> IssuesOf(SlideSpec slide) => SlideDeckSpecValidator.Validate(new SlideDeckSpec("Missions", [slide]), template, 0, 0, [TemplateSlideUse.EDIT]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon"), Text("Venus")], labels: ["Near", "Far", "Farther"])), Is.Empty, "Every copy takes content like a slot.");
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon"), Text("Venus"), Text("Jupiter")])), Has.Some.Contains("Slide 1 has 4 texts, but layout L1 has only 3."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text(new string('x', 120))])), Has.Some.Contains("Slide 1: text 2 needs about 5 lines, but only about 3 fit."), "A copy holds what its size allows.");
            Assert.That(IssuesOf(Edit(1, labels: ["Near", "Far"]) with { Removals = ["label 3", "text 3"] }), Is.Empty);
            Assert.That(IssuesOf(Edit(1, labels: ["Near"]) with { Removals = ["label 4"] }), Has.Some.Contains("Slide 1 removes \"label 4\", which template slide 1 does not have."));
            Assert.That(IssuesOf(Edit(1, texts: [Text("Mars"), Text("Moon")]) with { Removals = ["text 2"] }), Has.Some.Contains("Slide 1 fills text 2 and removes it as well."));
        }
    }

    [Test]
    public void TheTitleSlideAndTheChaptersUseTheChosenLayouts()
    {
        var chosen = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.TITLE] = "Title Slide",
            [LayoutPurpose.CHAPTER] = "Section Header",
        });

        var fitting = new SlideDeckSpec("Mars", [Slide("L1", "Mars"), Slide("L2", "Why Mars", startsChapter: true), Slide("L3", "Water", texts: [Text("Ice at the poles")])]);
        var deviating = new SlideDeckSpec("Mars", [Slide("L3", "Mars"), Slide("L3", "Why Mars", texts: [Text("Close to Earth")], startsChapter: true)]);

        var issues = SlideDeckSpecValidator.Validate(deviating, TEMPLATE, 0, 0, chosenLayouts: chosen);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(fitting, TEMPLATE, 0, 0, chosenLayouts: chosen), Is.Empty);
            Assert.That(issues, Has.Some.Contains("Slide 1 is the title slide, but uses layout L3. The title slide uses layout L1."));
            Assert.That(issues, Has.Some.Contains("Slide 2 starts a chapter, but uses layout L3. Chapters start with a slide on layout L2; put their content on the slides that follow."));
            Assert.That(SlideDeckSpecValidator.Validate(deviating, TEMPLATE, 0, 0), Is.Empty, "Without choices, the model decides every layout.");
        }
    }

    [Test]
    public void TheSlidesWhichAChoiceAddsAreThereAndTheImprintComesLast()
    {
        var chosen = ChosenLayouts.Of(TEMPLATE, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.TABLE_OF_CONTENTS] = "Two Content",
            [LayoutPurpose.IMPRINT] = "Table",
        });

        var title = Slide("L1", "Mars");
        var contents = Slide("L4", "Contents", texts: [Text("Why Mars"), Text("How to get there")]);
        var imprint = Slide("L6", "Imprint", table: new SlideTableSpec(["Role", "Name"], [["Author", "Jane Doe"]]));
        var content = Slide("L3", "Why Mars", texts: [Text("Close to Earth")]);

        var missing = SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [title, content]), TEMPLATE, 0, 0, chosenLayouts: chosen);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [title, contents, content, imprint]), TEMPLATE, 0, 0, chosenLayouts: chosen), Is.Empty);
            Assert.That(missing, Has.Some.Contains("The presentation has no slide on layout L4 for the table of contents, which lists the topics or chapters of the presentation without times. Add one right after the title slide."));
            Assert.That(missing, Has.Some.Contains("The presentation has no slide on layout L6 for the imprint"));
            Assert.That(SlideDeckSpecValidator.Validate(new SlideDeckSpec("Mars", [title, contents, imprint, content]), TEMPLATE, 0, 0, chosenLayouts: chosen), Has.Some.Contains("The imprint on layout L6 is not the last slide."));
        }
    }

    [Test]
    public void ASlideHasNoMoreMainPointsThanAskedFor()
    {
        var points = Text("Close to Earth", "Has water ice");
        var morePoints = new SlideTextBlock([new SlideParagraph("Has a day of 24.6 hours", 0), new SlideParagraph("Close to the day on Earth", 1)]);
        var deck = new SlideDeckSpec("Mars", [Slide("L1", "Mars"), Slide("L4", "Why Mars", texts: [points, morePoints])]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0, maxMainPointsPerSlide: 3), Is.Empty, "A sub-point belongs to its main point.");
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0, maxMainPointsPerSlide: 2), Has.Some.Contains("Slide 2 has 3 main points, but at most 2 per slide were asked for, all its texts together."), "The texts of a slide count together.");
            Assert.That(SlideDeckSpecValidator.Validate(deck, TEMPLATE, 0, 0), Is.Empty, "Without a maximum, the model chooses.");
        }
    }

    [Test]
    public void ATemplateSlideKeepsItsLayoutWhateverTheChoices()
    {
        var template = TemplateWithSlides();
        var chosen = ChosenLayouts.Of(template, new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.TITLE] = "Content",
            [LayoutPurpose.CHAPTER] = "Content",
        });

        var deck = new SlideDeckSpec("Weekly",
        [
            Keep(1, startsChapter: true),
            Edit(2, "News", texts: [Text("AI Studio: a new release", "EINSTEIN: a new model")]),
            Keep(3, startsChapter: true),
            Edit(4, texts: [Text("Thema:\tWeekly")]),
        ]);

        Assert.That(SlideDeckSpecValidator.Validate(deck, template, 0, 0, USES, chosen), Is.Empty);
    }

    [Test]
    public void EveryPictureForTheSlidesIsOnExactlyOneSlide()
    {
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L5", "The rocket", texts: [Text("Reusable")], pictures: [1]),
            Slide("L5", "The landing", texts: [Text("Soft")], pictures: [1]),
        ]);

        var issues = SlideDeckSpecValidator.Validate(deck, TEMPLATE, 2, 0);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Picture 1 is on slides 1, 2. Place it on exactly one of them."));
            Assert.That(issues, Has.Some.Contains("Picture 2 is on no slide. Place every picture of PICTURES on exactly one slide with a picture slot."));
        }
    }

    [Test]
    public void PicturesNeedNoPlaceInATemplateWithoutPictureSlots()
    {
        var deck = new SlideDeckSpec("Weekly", [Slide("L2", texts: [Text("Close to Earth")])]);

        Assert.That(SlideDeckSpecValidator.Validate(deck, TemplateWithSlides(), 2, 0), Is.Empty, "No repair round can find a place the template does not have.");
    }

    [Test]
    public void APictureOfTheLibraryIsUsedOnceButAnIconMayRepeat()
    {
        // Picture 1 is for the slides; pictures 2 to 4 come from the library, and 3 and 4 are icons:
        var library = new LibraryPictures(3, new HashSet<int> { 3, 4 });
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L5", "One", texts: [Text("Start")], pictures: [1]),
            Slide("L5", "Two", texts: [Text("Rocket")], pictures: [2]),
            Slide("L5", "Three", texts: [Text("Rocket again")], pictures: [2]),
            Slide("L5", "Four", texts: [Text("Risk")], pictures: [3]),
            Slide("L5", "Five", texts: [Text("Another risk")], pictures: [3]),
            Slide("L5", "Six", texts: [Text("Goal")], pictures: [4]),
        ]);

        var issues = SlideDeckSpecValidator.Validate(deck, TEMPLATE, 1, 0, libraryPictures: library);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Picture 2 of PICTURE_LIBRARY is on slides 2, 3. Use a picture of the library at most once; only icons may repeat."));
            Assert.That(issues, Has.None.Contains("Picture 3 of PICTURE_LIBRARY"), "An icon may mark several slides.");
            Assert.That(issues, Has.None.Contains("refers to picture"), "The numbers of the library follow those of the pictures for the slides.");
            Assert.That(issues, Has.None.Contains("is on no slide"), "Only the pictures for the slides have to be used.");
        }
    }

    [Test]
    public void AnIconNeverFillsABackdrop()
    {
        var template = TestTemplate.Read(
            new TestLayout("Backdrop", TestPlaceholder.Picture(11, new SlotBounds(0, 0, TestTemplate.SLIDE_WIDTH, TestTemplate.SLIDE_HEIGHT)), TestPlaceholder.Title()),
            new TestLayout("Picture with text", TestPlaceholder.Title(), TestPlaceholder.Text(1, new SlotBounds(838_200, 1_825_625, 5_150_000, 4_351_338)), TestPlaceholder.Picture(20, new SlotBounds(6_200_000, 1_825_625, 5_150_000, 3_500_000))));

        // Both pictures come from the library, and the first one is an icon:
        var library = new LibraryPictures(2, new HashSet<int> { 1 });
        var deck = new SlideDeckSpec("Mars",
        [
            Slide("L1", "Chapter", pictures: [1]),
            Slide("L2", "Goal", texts: [Text("Reach Mars")], pictures: [1]),
            Slide("L1", "Landscape", pictures: [2]),
        ]);

        var issues = SlideDeckSpecValidator.Validate(deck, template, 0, 0, libraryPictures: library);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issues, Has.Some.Contains("Slide 1 puts icon 1 into picture 1, which is a backdrop."));
            Assert.That(issues, Has.None.Contains("Slide 2 puts icon"), "A smaller picture slot is the place for an icon.");
            Assert.That(issues, Has.None.Contains("Slide 3 puts icon"), "A photo fills a backdrop well.");
        }
    }

    [Test]
    public void ADeckStillFitsATemplateWhichChangedBesideIt()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Slide("L2", texts: [Text("Close to Earth")]), Edit(4, texts: [Text("Thema:\tWeekly")])]);
        var basis = TemplateWithSlides().DescribeBasisOf(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(basis.Keys, Is.EquivalentTo(new[] { "template slide 1", "L2", "template slide 4" }), "An edited template slide fills the slots of its layout, a kept one does not.");
            Assert.That(SlideDeckSpecValidator.StillFits(deck, TemplateWithSlides(), basis), Is.True, "The same file, read once more.");
            Assert.That(SlideDeckSpecValidator.StillFits(deck, ChangedTemplateWithSlides(newLayoutAt: 3), basis), Is.True, "A new layout at the end of the slide master leaves the IDs of the others alone.");
        }
    }

    [Test]
    public void ADeckNoLongerFitsWhenItsLayoutsOrTemplateSlidesMove()
    {
        var deck = new SlideDeckSpec("Weekly", [Keep(1), Slide("L2", texts: [Text("Close to Earth")]), Keep(3)]);
        var basis = TemplateWithSlides().DescribeBasisOf(deck);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SlideDeckSpecValidator.StillFits(deck, ChangedTemplateWithSlides(newLayoutAt: 1), basis), Is.False, "L2 is another layout now.");
            Assert.That(SlideDeckSpecValidator.StillFits(deck, ChangedTemplateWithSlides(deletedSlide: 3), basis), Is.False, "Template slide 3 is the imprint now.");
            Assert.That(SlideDeckSpecValidator.StillFits(deck, ChangedTemplateWithSlides(deletedSlide: 3), new Dictionary<string, string>()), Is.False, "Without a basis, nothing is known to fit.");
        }
    }

    /// <summary>
    /// What the user chose for the slides of TemplateWithSlides: the second slide and
    /// the imprint are edited, and the closing slide is left out.
    /// </summary>
    private static readonly TemplateSlideUse[] USES = [TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.KEEP, TemplateSlideUse.EDIT, TemplateSlideUse.OMIT];

    /// <summary>
    /// A template with a title slide, two content slides, an imprint, and a closing slide on a
    /// layout without room for content. Its families are L1 for the title and L2 for the content.
    /// </summary>
    private static PresentationTemplate TemplateWithSlides() => ChangedTemplateWithSlides();

    /// <summary>
    /// The template of TemplateWithSlides after its author changed it.
    /// </summary>
    /// <param name="newLayoutAt">Where the slide master gets a new layout with room for content, starting at 0; null for no new layout.</param>
    /// <param name="deletedSlide">The number of the slide the author deleted; 0 for none.</param>
    private static PresentationTemplate ChangedTemplateWithSlides(int? newLayoutAt = null, int deletedSlide = 0)
    {
        List<TestLayout> layouts =
        [
            new("Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(new SlotBounds(838_200, 3_602_038, 10_515_600, 1_655_762))),
            new("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)),
            new("Title Only", TestPlaceholder.Title()),
        ];

        List<TestSlide> slides =
        [
            new(0, (TestPlaceholder.Title(), "Consultation hour")),
            new(1, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:")),
            new(1, (TestPlaceholder.Title(), "Questions")),
            new(1, (TestPlaceholder.Title(), "Impressum"), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour")),
            new(2, (TestPlaceholder.Title(), "Thank you")),
        ];

        if (newLayoutAt is { } position)
        {
            layouts.Insert(position, new TestLayout("Imprint", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)));
            slides = [..slides.Select(slide => slide.Layout >= position ? slide with { Layout = slide.Layout + 1 } : slide)];
        }

        if (deletedSlide > 0)
            slides.RemoveAt(deletedSlide - 1);

        using var file = TestTemplate.Create(new TestTemplateOptions(Slides: slides), [..layouts]);
        return PresentationTemplate.Read(file);
    }

    private static SlideSpec Keep(int templateSlide, bool startsChapter = false) => new(string.Empty, startsChapter, string.Empty, string.Empty, [], [], [], null, templateSlide);

    private static SlideSpec Edit(int templateSlide, string title = "", string subtitle = "", IReadOnlyList<SlideTextBlock>? texts = null, IReadOnlyList<string>? labels = null) => new(string.Empty, false, title, subtitle, texts ?? [], labels ?? [], [], null, templateSlide);

    private static SlideSpec Slide(string layout, string title = "Title", string subtitle = "", IReadOnlyList<SlideTextBlock>? texts = null, IReadOnlyList<int>? pictures = null, SlideTableSpec? table = null, bool startsChapter = false) => new(layout, startsChapter, title, subtitle, texts ?? [], [], pictures ?? [], table);

    private static SlideTextBlock Text(params string[] paragraphs) => new(paragraphs.Select(paragraph => new SlideParagraph(paragraph, 0)).ToList());
}