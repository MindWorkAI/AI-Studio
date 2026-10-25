using AIStudio.Tools.PowerPoint;

using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks how a template is read: which layouts become families, and what the model is told about them.
/// </summary>
[TestFixture]
public sealed class PresentationTemplateTests
{
    private static readonly SlotBounds SUBTITLE = new(838_200, 3_602_038, 10_515_600, 1_655_762);
    private static readonly SlotBounds FULL_TEXT = TestTemplate.MASTER_BODY_BOUNDS;
    private static readonly SlotBounds LEFT_TEXT = new(838_200, 1_825_625, 5_150_000, 4_351_338);
    private static readonly SlotBounds RIGHT_TEXT = new(6_200_000, 1_825_625, 5_150_000, 4_351_338);
    private static readonly SlotBounds RIGHT_PICTURE = new(6_200_000, 1_825_625, 5_150_000, 3_500_000);
    private static readonly SlotBounds LABEL_BELOW_RIGHT_PICTURE = new(6_200_000, 5_375_625, 5_150_000, 300_000);
    private static readonly SlotBounds LABEL_ABOVE_FULL_TEXT = new(838_200, 1_400_000, 3_000_000, 300_000);

    [Test]
    public void TheColorVariantsOfALayoutBecomeOneFamily()
    {
        var template = TestTemplate.Read(
            new TestLayout("A Title 1", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("B Title 1", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("A Content 1", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)),
            new TestLayout("B Content 1", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)),
            new TestLayout("C Content 1", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        Assert.Multiple(() =>
        {
            Assert.That(template.Families.Select(family => family.Id), Is.EqualTo(new[] { "L1", "L2" }));
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Title 1", "Content 1" }), "The model reads the name the variants share, without the word which tells them apart.");
            Assert.That(template.Families[1].Variants.Select(variant => variant.Label), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(template.VariantLabels, Is.EqualTo(new[] { "A", "B", "C" }));
        });
    }

    [Test]
    public void LayoutsWhichOnlyLookAlikeAreNoVariants()
    {
        // The DLR template carries "Tabelle/Table 1" and "Tabelle/Table 2" in three colors each. All six
        // offer a title and a table at the same places, yet only the colors are variants:
        var template = TestTemplate.Read(
            new TestLayout("A Table 1", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)),
            new TestLayout("A Table 2", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)),
            new TestLayout("B Table 1", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)),
            new TestLayout("B Table 2", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)),
            new TestLayout("C Table 1", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)));

        Assert.Multiple(() =>
        {
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Table 1", "Table 2" }));
            Assert.That(template.Families[0].Variants.Select(variant => variant.Label), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(template.Families[1].Variants.Select(variant => variant.Label), Is.EqualTo(new[] { "A", "B" }));
        });
    }

    [Test]
    public void AVariantNamedAtTheEndIsFoundAsWell()
    {
        var template = TestTemplate.Read(
            new TestLayout("Title blue", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("Title green", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("Content blue", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)),
            new TestLayout("Content green", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        Assert.Multiple(() =>
        {
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Title", "Content" }));
            Assert.That(template.VariantLabels, Is.EqualTo(new[] { "blue", "green" }));
        });
    }

    [Test]
    public void LayoutsWithoutVariantsKeepTheirNames()
    {
        var template = TestTemplate.Read(
            new TestLayout("Title Slide", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("Two Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, LEFT_TEXT), TestPlaceholder.Text(2, RIGHT_TEXT)));

        Assert.Multiple(() =>
        {
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Title Slide", "Two Content" }));
            Assert.That(template.Families.SelectMany(family => family.Variants).Select(variant => variant.Label), Is.All.Null);
            Assert.That(template.VariantLabels, Is.Empty);
        });
    }

    [Test]
    public void ALayoutWithoutRoomForContentIsLeftOut()
    {
        var template = TestTemplate.Read(
            new TestLayout("Title Only", TestPlaceholder.Title()),
            new TestLayout("Blank"),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Content" }), "A sample page or a blank page offers the model nothing to fill.");
    }

    [Test]
    public void ASmallTextPlaceholderIsALabel()
    {
        var template = TestTemplate.Read(
            new TestLayout("Content with heading", TestPlaceholder.Title(), TestPlaceholder.Label(17, LABEL_ABOVE_FULL_TEXT), TestPlaceholder.Text(1, FULL_TEXT)));

        Assert.That(template.Families[0].Slots.Select(slot => slot.Role), Is.EqualTo(new[] { PlaceholderRole.TITLE, PlaceholderRole.LABEL, PlaceholderRole.TEXT }));
    }

    [Test]
    public void APlaceholderWithoutPlaceTakesThePlaceOfTheMaster()
    {
        var template = TestTemplate.Read(
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        var title = template.Families[0].Slots[0];
        Assert.Multiple(() =>
        {
            Assert.That(title.Role, Is.EqualTo(PlaceholderRole.TITLE));
            Assert.That(title.Bounds, Is.EqualTo(TestTemplate.MASTER_TITLE_BOUNDS));
        });
    }

    [Test]
    public void SlotsAreNumberedInReadingOrder()
    {
        // The file lists the right text first, but a reader starts on the left:
        var template = TestTemplate.Read(
            new TestLayout("Two Content", TestPlaceholder.Title(), TestPlaceholder.Text(2, RIGHT_TEXT), TestPlaceholder.Text(1, LEFT_TEXT)));

        var texts = template.Families[0].Slots.Where(slot => slot.Role is PlaceholderRole.TEXT).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(texts.Select(slot => slot.Ordinal), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(texts.Select(slot => slot.Bounds), Is.EqualTo(new[] { LEFT_TEXT, RIGHT_TEXT }));
        });
    }

    [Test]
    public void TheCapacityFollowsSizeAndFontSize()
    {
        // At 20 pt, a line takes 304,800 EMU and a character about 127,000 EMU. The box is sized for
        // 10.5 lines and 50.5 characters, plus the default insets, so that no rounding decides:
        var bounds = new SlotBounds(838_200, 1_825_625, 6_413_500 + 2 * 91_440, 3_200_400 + 2 * 45_720);
        var template = TestTemplate.Read(
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, bounds, 2000)));

        Assert.That(template.Families[0].Slots[1].Capacity, Is.EqualTo(new TextCapacity(10, 50)));
    }

    [Test]
    public void TheCatalogDescribesEverySlotInWords()
    {
        var template = TestTemplate.Read(
            new TestLayout("Picture with text",
                TestPlaceholder.Title(),
                TestPlaceholder.Text(1, LEFT_TEXT, 2000),
                TestPlaceholder.Picture(20, RIGHT_PICTURE),
                TestPlaceholder.Label(16, LABEL_BELOW_RIGHT_PICTURE, 1200)));

        Assert.That(template.ToCatalogPrompt(), Is.EqualTo(
            """- L1 "Picture with text": title (up to 1 line of about 36 characters); text 1 (left, middle, up to 13 lines of about 39 characters); picture 1 (right, middle, landscape); label 1 (below picture 1, up to 1 line of about 65 characters)"""));
    }

    [Test]
    public void APictureSlotOfAtLeastHalfTheSlideIsABackdrop()
    {
        var template = TestTemplate.Read(
            new TestLayout("Backdrop", TestPlaceholder.Picture(11, new SlotBounds(0, 0, TestTemplate.SLIDE_WIDTH, TestTemplate.SLIDE_HEIGHT)), TestPlaceholder.Title()),
            new TestLayout("Picture with text", TestPlaceholder.Title(), TestPlaceholder.Text(1, LEFT_TEXT, 2000), TestPlaceholder.Picture(20, RIGHT_PICTURE)));

        var catalog = template.ToCatalogPrompt();
        Assert.Multiple(() =>
        {
            Assert.That(catalog, Does.Contain("landscape, backdrop)"), "The model learns where an icon must not go.");
            Assert.That(catalog, Does.Contain("picture 1 (right, middle, landscape)"));
        });
    }

    [Test]
    public void WithoutATemplateTheCommonLayoutsAreOffered()
    {
        var template = PresentationTemplate.CreateDefault();

        Assert.Multiple(() =>
        {
            Assert.That(template.Families.Select(family => family.Name), Is.EqualTo(new[] { "Title Slide", "Section Header", "Title and Content", "Two Content", "Content with Picture", "Table" }));
            Assert.That(template.VariantLabels, Is.Empty);
            Assert.That(template.ToCatalogPrompt(), Does.Contain("""- L3 "Title and Content": title (up to 1 line of about 36 characters); text 1 (full width, middle, up to 9 lines of about 58 characters)"""));
        });
    }

    [Test]
    public void AFamilyIsFoundByTheIdTheModelWrote()
    {
        var template = TestTemplate.Read(
            new TestLayout("A Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)),
            new TestLayout("B Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        var family = template.FindFamily(" l1 ");
        Assert.Multiple(() =>
        {
            Assert.That(family, Is.Not.Null, "Models write IDs in whatever case they like.");
            Assert.That(family?.GetLayout("B").Name, Is.EqualTo("B Content"));
            Assert.That(family?.GetLayout("Z").Name, Is.EqualTo("A Content"), "A variant the family lacks falls back to its first one.");
            Assert.That(template.FindFamily("L9"), Is.Null);
        });
    }

    [Test]
    public void TheSlidesOfATemplateAreReadWithWhatTheirSlotsHold()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Consultation hour"), (new TestPlaceholder(P.PlaceholderValues.SubTitle, 1), "Every Monday\n10:00 to 10:45")),
                new TestSlide(2, (TestPlaceholder.Title(), "News"), (new TestPlaceholder(null, 1), "AI Studio:\nEINSTEIN:"), (new TestPlaceholder(P.PlaceholderValues.Footer, 12), "Team, 2026-10-12")) { IsHidden = true },
            ]),
            new TestLayout("A Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(SUBTITLE)),
            new TestLayout("A Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT), TestPlaceholder.Footer(12, LABEL_BELOW_RIGHT_PICTURE)),
            new TestLayout("B Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT), TestPlaceholder.Footer(12, LABEL_BELOW_RIGHT_PICTURE)));

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.Slides.Select(slide => slide.Number), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(template.Slides.Select(slide => slide.Title), Is.EqualTo(new[] { "Consultation hour", "News" }));
            Assert.That(template.Slides.Select(slide => slide.FamilyId), Is.EqualTo(new[] { "L1", "L2" }), "A slide on the second color of a layout belongs to the layout's family all the same.");
            Assert.That(template.Slides[0].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "Consultation hour"), (PlaceholderRole.SUBTITLE, "Every Monday\n10:00 to 10:45") }));
            Assert.That(template.Slides[1].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "News"), (PlaceholderRole.TEXT, "AI Studio:\nEINSTEIN:") }), "The footer is no slot of the slide.");
            Assert.That(template.Slides.Select(slide => slide.IsHidden), Is.EqualTo(new[] { false, true }));
            Assert.That(template.FindSlide(2), Is.SameAs(template.Slides[1]));
            Assert.That(template.FindSlide(0), Is.Null);
            Assert.That(template.FindSlide(3), Is.Null);
        });
    }

    [Test]
    public void APlaceholderOfASlideFindsItsSlotByIndexWhateverTypeTheSlideStates()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (new TestPlaceholder(P.PlaceholderValues.CenteredTitle), "Questions"), (new TestPlaceholder(null, 13), "Image by somebody")),
            ]),
            new TestLayout("Picture", TestPlaceholder.Title(), TestPlaceholder.Picture(11, RIGHT_PICTURE), TestPlaceholder.Label(13, LABEL_BELOW_RIGHT_PICTURE)));

        var template = PresentationTemplate.Read(file);
        Assert.That(template.Slides[0].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "Questions"), (PlaceholderRole.LABEL, "Image by somebody") }));
    }

    [Test]
    public void AFreeTableIsTheContentOfTheTableSlot()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Costs")) { Elements = [TestElement.Table(FULL_TEXT, ["Item", "Cost"], ["Fuel", "10"])] },
                new TestSlide(0, (TestPlaceholder.Title(), "Costs"), (new TestPlaceholder(P.PlaceholderValues.Table, 11), string.Empty)) { Elements = [TestElement.Table(FULL_TEXT, ["Item", "Cost"], ["Food", "5"])] },
            ]),
            new TestLayout("Table", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)));

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.Slides[0].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "Costs"), (PlaceholderRole.TABLE, "Item | Cost\nFuel | 10") }), "A table pasted onto the slide is the table of the slide.");
            Assert.That(template.Slides[1].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "Costs"), (PlaceholderRole.TABLE, "Item | Cost\nFood | 5") }), "An empty table placeholder makes way for the table the slide shows.");
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.EDIT, TemplateSlideUse.KEEP]), Is.EqualTo(
                """
                - Template slide 1, to edit, with the slots of layout L1: title "Costs"; table 1 "Item | Cost / Fuel | 10"
                - Template slide 2, to keep unchanged: title "Costs"; table 1 "Item | Cost / Food | 5"
                """.ReplaceLineEndings("\n")), "The model learns that the slide has a table.");
        });
    }

    [Test]
    public void ATablePlaceholderWinsOverAFreeTable()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Costs")) { Elements = [TestElement.Table(FULL_TEXT, ["Free"], ["Table"]), TestElement.TablePlaceholder(11, FULL_TEXT, ["Placeholder"], ["Table"])] },
            ]),
            new TestLayout("Table", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)));

        var template = PresentationTemplate.Read(file);
        Assert.That(template.Slides[0].Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[] { (PlaceholderRole.TITLE, "Costs"), (PlaceholderRole.TABLE, "Placeholder\nTable") }));
    }

    [Test]
    public void ASlotTheSlideResizesTakesItsSizeOnTheSlide()
    {
        // The layout slot holds 10 lines of 50 characters, see TheCapacityFollowsSizeAndFontSize:
        var bounds = new SlotBounds(838_200, 1_825_625, 6_413_500 + 2 * 91_440, 3_200_400 + 2 * 45_720);
        var half = bounds with { Width = bounds.Width / 2, Height = bounds.Height / 2 };
        var nudged = bounds with { X = bounds.X + 50_000 };
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Rocket"), (TestPlaceholder.Text(1, half), "Reusable")),
                new TestSlide(0, (TestPlaceholder.Title(), "Fuel"), (TestPlaceholder.Text(1, nudged), "Cheap")),
                new TestSlide(0, (TestPlaceholder.Title(), "Crew"), (TestPlaceholder.Text(1, half), string.Empty)),
            ]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, bounds, 2000)));

        var template = PresentationTemplate.Read(file);
        var slots = template.Families[0].Slots;
        Assert.Multiple(() =>
        {
            Assert.That(template.Slides[0].MovedSlots.Select(slot => (slot.Role, slot.Ordinal, slot.Bounds, slot.Capacity)), Is.EqualTo(new[] { (PlaceholderRole.TEXT, 1, half, (TextCapacity?)new TextCapacity(5, 25)) }), "Half the size holds half the lines of half the characters.");
            Assert.That(template.Slides[0].OnSlide(slots[1]).Capacity, Is.EqualTo(new TextCapacity(5, 25)));
            Assert.That(template.Slides[0].OnSlide(slots[0]), Is.SameAs(slots[0]), "The title stays where the layout puts it.");
            Assert.That(template.Slides[1].MovedSlots, Is.Empty, "A placeholder dragged a little does not count as moved.");
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.EDIT, TemplateSlideUse.EDIT, TemplateSlideUse.EDIT]), Is.EqualTo(
                """
                - Template slide 1, to edit, with the slots of layout L1: title "Rocket"; text 1 (left, middle, up to 5 lines of about 25 characters, on this slide) "Reusable"
                - Template slide 2, to edit, with the slots of layout L1: title "Fuel"; text 1 "Cheap"
                - Template slide 3, to edit, with the slots of layout L1: title "Crew"; text 1 (left, middle, up to 5 lines of about 25 characters, on this slide, empty)
                """.ReplaceLineEndings("\n")));
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.KEEP]), Is.EqualTo("- Template slide 1, to keep unchanged: title \"Rocket\"; text 1 \"Reusable\""), "A slide to keep needs no sizes.");
        });
    }

    [Test]
    public void AFreeTableTellsWhereItsSlotStarts()
    {
        var lower = FULL_TEXT with { Y = FULL_TEXT.Y + 1_000_000 };
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Costs")) { Elements = [TestElement.Table(FULL_TEXT, ["Item", "Cost"], ["Fuel", "10"])] },
                new TestSlide(0, (TestPlaceholder.Title(), "Costs")) { Elements = [TestElement.Table(lower, ["Item", "Cost"], ["Fuel", "10"])] },
            ]),
            new TestLayout("Table", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)));

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.Slides[0].MovedSlots, Is.Empty, "A table grows with its rows, so a short one at the place of its slot does not shrink the slot.");
            Assert.That(template.Slides[1].MovedSlots.Select(slot => slot.Bounds), Is.EqualTo(new[] { lower with { Height = FULL_TEXT.Bottom - lower.Y } }), "A table placed lower leaves less room, down to the bottom of its slot.");
        });
    }

    [Test]
    public void TheElementsOfASlideAreReadFromBackToFront()
    {
        var line = new SlotBounds(838_200, 6_000_000, 5_000_000, 0);
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Costs"), (new TestPlaceholder(P.PlaceholderValues.Footer, 12), "Team"))
                {
                    Elements =
                    [
                        TestElement.Table(FULL_TEXT, ["Item"], ["Fuel"]),
                        TestElement.Picture(RIGHT_PICTURE, 1, 2, 3),
                        TestElement.TextBox(LABEL_BELOW_RIGHT_PICTURE, "Image by somebody"),
                        TestElement.TextBox(LABEL_ABOVE_FULL_TEXT, string.Empty),
                        TestElement.Rectangle(RIGHT_TEXT),
                        TestElement.Line(line),
                        TestElement.Group(LEFT_TEXT, TestElement.TextBox(LEFT_TEXT, "A"), TestElement.TextBox(LEFT_TEXT, "B")),
                        TestElement.Table(RIGHT_TEXT, ["Second"], ["Table"]),
                        TestElement.Chart(LEFT_TEXT),
                    ],
                },
            ]),
            new TestLayout("Table", TestPlaceholder.Title(), TestPlaceholder.Table(11, FULL_TEXT)));

        var elements = PresentationTemplate.Read(file).Slides[0].Elements;
        Assert.Multiple(() =>
        {
            Assert.That(elements.Select(element => (element.Number, element.ShapeId, element.Kind, element.Text)), Is.EqualTo(new[]
            {
                (1, 5U, TemplateSlideElementKind.PICTURE, string.Empty),
                (2, 6U, TemplateSlideElementKind.TEXT_BOX, "Image by somebody"),
                (3, 8U, TemplateSlideElementKind.SHAPE, string.Empty),
                (4, 9U, TemplateSlideElementKind.LINE, string.Empty),
                (5, 10U, TemplateSlideElementKind.GROUP, "A\nB"),
                (6, 11U, TemplateSlideElementKind.TABLE, "Second\nTable"),
                (7, 12U, TemplateSlideElementKind.CHART, string.Empty),
            }), "The footer belongs to the presentation, the empty text box shows nothing, and the first table is the content of the table slot.");
            Assert.That(elements.Select(element => element.Bounds), Is.EqualTo(new SlotBounds?[] { RIGHT_PICTURE, LABEL_BELOW_RIGHT_PICTURE, RIGHT_TEXT, line, LEFT_TEXT, RIGHT_TEXT with { Height = 2 * 370_840 }, LEFT_TEXT }));
            Assert.That(elements.Select(element => element.Name), Has.Exactly(1).EqualTo("element 3"));
        });
    }

    [Test]
    public void AnEntryToEditListsTheElementsOfTheSlide()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Rocket"), (new TestPlaceholder(null, 1), "Reusable"))
                {
                    Elements =
                    [
                        TestElement.Picture(RIGHT_PICTURE, 1, 2, 3),
                        TestElement.TextBox(LABEL_BELOW_RIGHT_PICTURE, "Bildquelle hier angeben"),
                        TestElement.TextBox(new SlotBounds(1_000_000, 3_000_000, 3_000_000, 500_000), "Old note"),
                        TestElement.Rectangle(new SlotBounds(0, 0, TestTemplate.SLIDE_WIDTH, TestTemplate.SLIDE_HEIGHT)),
                    ],
                },
                new TestSlide(0, (TestPlaceholder.Title(), "Chart")) { Elements = [TestElement.Chart(LEFT_TEXT)] },
            ]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, LEFT_TEXT)));

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.EDIT, TemplateSlideUse.EDIT]), Is.EqualTo(
                """
                - Template slide 1, to edit, with the slots of layout L1: title "Rocket"; text 1 "Reusable"; element 1 (picture, right, middle); element 2 (text box, right, bottom) "Bildquelle hier angeben"; element 3 (text box, left, middle, overlaps text 1) "Old note"; element 4 (shape, covering most of the slide)
                - Template slide 2, to edit, with the slots of layout L1: title "Chart"; element 1 (chart, left, middle, overlaps text 1)
                """.ReplaceLineEndings("\n")), "A slot the slide leaves out takes the place of the layout, where filling it would cover the chart.");
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.KEEP]), Is.EqualTo("- Template slide 1, to keep unchanged: title \"Rocket\"; text 1 \"Reusable\""), "A slide to keep has nothing to remove.");
        });
    }

    [Test]
    public void CopiesOfAHeadingAndItsTextBecomeSlotsOfTheSlide()
    {
        using var file = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout());

        var slide = PresentationTemplate.Read(file).Slides[0];
        Assert.Multiple(() =>
        {
            Assert.That(slide.SlotCopies.Select(copy => (copy.Slot.Role, copy.Slot.Ordinal, copy.Slot.Bounds, copy.ShapeId)), Is.EqualTo(new[]
            {
                (PlaceholderRole.LABEL, 2, StackedHeadings.SECOND_HEADING, 5U),
                (PlaceholderRole.TEXT, 2, StackedHeadings.SECOND_TEXT, 6U),
                (PlaceholderRole.LABEL, 3, StackedHeadings.THIRD_HEADING, 7U),
                (PlaceholderRole.TEXT, 3, StackedHeadings.THIRD_TEXT, 8U),
            }), "The copies are numbered after the slots of the layout, in reading order.");
            Assert.That(slide.SlotCopies.Select(copy => copy.Slot.Capacity), Is.EqualTo(new TextCapacity?[] { new TextCapacity(1, 14), new TextCapacity(3, 25), new TextCapacity(1, 18), new TextCapacity(3, 25) }), "A copy holds what its size allows, as a moved slot does.");
            Assert.That(slide.Slots.Select(slot => $"{slot.Role.CatalogName()} {slot.Ordinal}"), Is.EqualTo(new[] { "title 1", "label 1", "text 1", "label 2", "text 2", "label 3", "text 3" }));
            Assert.That(slide.Contents.Select(content => (content.Slot.Role, content.Text)), Is.EqualTo(new[]
            {
                (PlaceholderRole.TITLE, "Missions"),
                (PlaceholderRole.LABEL, "Lorem ipsum"),
                (PlaceholderRole.TEXT, "Ut wisi enim"),
                (PlaceholderRole.LABEL, "Lorem"),
                (PlaceholderRole.TEXT, "Duis autem"),
                (PlaceholderRole.LABEL, "Nam liber"),
                (PlaceholderRole.TEXT, "Consectetuer"),
            }));
            Assert.That(slide.Elements.Select(element => (element.Kind, element.Text)), Is.EqualTo(new[] { (TemplateSlideElementKind.PICTURE, string.Empty), (TemplateSlideElementKind.TEXT_BOX, "Bildquelle hier angeben") }), "The source of the picture has the height of a heading, yet lines up with none.");
        });
    }

    [Test]
    public void OnlyAShapeOfTheSizeOfASlotInLineWithItIsACopy()
    {
        var twin = new SlotBounds(838_200, 4_100_000, 3_400_000, 1_000_000);
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Missions"), (new TestPlaceholder(P.PlaceholderValues.Body, 17), "Lorem ipsum"), (TestPlaceholder.Text(1, StackedHeadings.FIRST_TEXT), "Ut wisi enim"))
                {
                    Elements =
                    [
                        TestElement.TextBox(new SlotBounds(838_200, 1_900_000, 3_400_000, 1_000_000), "On text 1"),
                        TestElement.TextBox(new SlotBounds(838_200, 3_000_000, 2_000_000, 1_000_000), "Other width"),
                        TestElement.TextBox(twin, "Twin"),
                        TestElement.TextBox(twin, "Twin"),
                        new TestElement((_, slidePart) => TestElement.TextBox(new SlotBounds(838_200, 5_200_000, 3_400_000, 1_000_000), "Same ID").Create(2, slidePart)),
                        TestElement.TextBox(new SlotBounds(4_400_000, 1_825_625, 3_400_000, 1_000_000), string.Empty),
                        TestElement.TextBox(StackedHeadings.PICTURE_SOURCE, "Bildquelle hier angeben"),
                    ],
                },
            ]),
            StackedHeadings.Layout());

        var slide = PresentationTemplate.Read(file).Slides[0];
        Assert.Multiple(() =>
        {
            Assert.That(slide.SlotCopies.Select(copy => (copy.Slot.Role, copy.Slot.Ordinal, copy.Slot.Bounds)), Is.EqualTo(new[] { (PlaceholderRole.TEXT, 2, twin) }), "Of two copies in one place, the first counts.");
            Assert.That(slide.Elements.Select(element => element.Text), Is.EqualTo(new[] { "On text 1", "Other width", "Twin", "Bildquelle hier angeben" }), "A box lying on a slot, a text of another width, the second twin and a box in no line with a slot stay elements; one whose ID the title shares and an empty one are neither.");
        });
    }

    [Test]
    public void CopiesOfCopiesFormAGrid()
    {
        // The upper right copy lines up with nothing but the lower right one, which lines up with text 1:
        var text = new SlotBounds(838_200, 3_500_000, 3_400_000, 1_000_000);
        var lowerRight = text with { X = 4_400_000 };
        var upperRight = lowerRight with { Y = 2_300_000 };
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), "Missions"), (TestPlaceholder.Text(1, text), "Ut wisi enim"))
                {
                    Elements = [TestElement.TextBox(upperRight, "Above"), TestElement.TextBox(lowerRight, "Beside")],
                },
            ]),
            StackedHeadings.Layout());

        var slide = PresentationTemplate.Read(file).Slides[0];
        Assert.Multiple(() =>
        {
            Assert.That(slide.SlotCopies.Select(copy => (copy.Slot.Ordinal, copy.Slot.Bounds)), Is.EqualTo(new[] { (2, upperRight), (3, lowerRight) }));
            Assert.That(slide.Slots.Select(slot => $"{slot.Role.CatalogName()} {slot.Ordinal}"), Is.EqualTo(new[] { "title 1", "label 1", "text 2", "text 1", "text 3" }), "The slots of the layout keep their numbers; a copy goes where a reader comes across it.");
        });
    }

    [Test]
    public void ALabelRightAboveATextIsItsHeadingEvenRightBelowAnother()
    {
        var upper = new SlotBounds(838_200, 1_825_625, 5_150_000, 1_500_000);
        var heading = new SlotBounds(838_200, 3_350_000, 3_000_000, 300_000);
        var lower = new SlotBounds(838_200, 3_775_625, 5_150_000, 1_500_000);
        var template = TestTemplate.Read(new TestLayout("Two texts", TestPlaceholder.Title(), TestPlaceholder.Text(1, upper), TestPlaceholder.Label(17, heading), TestPlaceholder.Text(2, lower)));

        Assert.That(template.ToCatalogPrompt(), Does.Contain("label 1 (above text 2,"), "The heading is closer to the text above it, yet introduces the one below it.");
    }

    [Test]
    public void AnEntryToEditOffersTheCopiesAsSlots()
    {
        using var file = TestTemplate.Create(new TestTemplateOptions(Slides: [StackedHeadings.Slide()]), StackedHeadings.Layout());

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.EDIT]), Is.EqualTo(
                """
                - Template slide 1, to edit, with the slots of layout L1: title "Missions"; label 1 "Lorem ipsum"; text 1 (left, middle, up to 3 lines of about 25 characters, on this slide) "Ut wisi enim"; label 2 (above text 2, up to 1 line of about 14 characters, on this slide) "Lorem"; text 2 (left, middle, up to 3 lines of about 25 characters, on this slide) "Duis autem"; label 3 (above text 3, up to 1 line of about 18 characters, on this slide) "Nam liber"; text 3 (left, bottom, up to 3 lines of about 25 characters, on this slide) "Consectetuer"; element 1 (picture, right, middle); element 2 (text box, right, bottom) "Bildquelle hier angeben"
                """));
            Assert.That(template.ToTemplateSlidesPrompt([TemplateSlideUse.KEEP]), Is.EqualTo("- Template slide 1, to keep unchanged: title \"Missions\"; label 1 \"Lorem ipsum\"; text 1 \"Ut wisi enim\"; label 2 \"Lorem\"; text 2 \"Duis autem\"; label 3 \"Nam liber\"; text 3 \"Consectetuer\""), "A slide to keep says what its copies hold, so that no new slide repeats it.");
        });
    }

    [Test]
    public void ASlideOnALayoutWithoutRoomForContentBelongsToNoFamily()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides: [new TestSlide(1, (TestPlaceholder.Title(), "Thank you"))]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)),
            new TestLayout("Title Only", TestPlaceholder.Title()));

        var template = PresentationTemplate.Read(file);
        Assert.Multiple(() =>
        {
            Assert.That(template.Slides[0].FamilyId, Is.Null);
            Assert.That(template.Slides[0].Title, Is.EqualTo("Thank you"), "What the slide holds is read all the same, so the user knows which slide it is.");
        });
    }
}