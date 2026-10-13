using AIStudio.Tools.PowerPoint;

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
}