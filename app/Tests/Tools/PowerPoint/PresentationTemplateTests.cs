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
            Assert.That(template.Slides.Select(slide => slide.IsImprint), Is.EqualTo(new[] { false, false }));
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

    [Test]
    public void AnImprintIsFoundByItsTitleOrByItsLines()
    {
        using var file = TestTemplate.Create(
            new TestTemplateOptions(Slides:
            [
                new TestSlide(0, (TestPlaceholder.Title(), " impressum "), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour")),
                new TestSlide(0, (TestPlaceholder.Title(), "About this presentation"), (new TestPlaceholder(null, 1), "Thema:\tConsultation hour\nDatum:\t2026-10-12\nAutoren:\tThe team\nBildquellen:\tPixabay")),
                new TestSlide(0, (TestPlaceholder.Title(), "Agenda"), (new TestPlaceholder(null, 1), "Datum: today\nThema: news")),
            ]),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, FULL_TEXT)));

        var template = PresentationTemplate.Read(file);
        Assert.That(template.Slides.Select(slide => slide.IsImprint), Is.EqualTo(new[] { true, true, false }), "Two lines alike are no imprint yet.");
    }
}