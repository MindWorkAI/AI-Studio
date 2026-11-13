namespace AIStudio.Tools.PowerPoint;

internal sealed partial class PresentationTemplate
{
    /// <summary>
    /// Creates the catalog the Slide Assistant works with when no template was chosen.
    /// </summary>
    /// <remarks>
    /// Without a template, the Slide Assistant writes an outline only. It still asks the model for the
    /// same JSON, against these few layouts of a plain 16:9 presentation, so that one prompt and one
    /// check serve both cases. Places and font sizes are those of a new presentation in PowerPoint.
    /// </remarks>
    /// <returns>The catalog.</returns>
    public static PresentationTemplate CreateDefault()
    {
        var title = new SlotBounds(838_200, 365_125, 10_515_600, 1_325_563);
        var body = new SlotBounds(838_200, 1_825_625, 10_515_600, 4_351_338);
        var left = new SlotBounds(838_200, 1_825_625, 5_181_600, 4_351_338);
        var right = new SlotBounds(6_172_200, 1_825_625, 5_181_600, 4_351_338);

        TemplateLayout[] layouts =
        [
            DefaultLayout(1, "Title Slide", "title",
                (PlaceholderRole.TITLE, new SlotBounds(1_524_000, 1_122_363, 9_144_000, 2_387_600), 6000),
                (PlaceholderRole.SUBTITLE, new SlotBounds(1_524_000, 3_602_038, 9_144_000, 1_655_762), 2400)),

            DefaultLayout(2, "Section Header", "secHead",
                (PlaceholderRole.TITLE, new SlotBounds(831_850, 1_709_738, 10_515_600, 2_852_737), 6000),
                (PlaceholderRole.SUBTITLE, new SlotBounds(831_850, 4_589_463, 10_515_600, 1_500_187), 2400)),

            DefaultLayout(3, "Title and Content", "obj",
                (PlaceholderRole.TITLE, title, 4400),
                (PlaceholderRole.TEXT, body, 2800)),

            DefaultLayout(4, "Two Content", "twoObj",
                (PlaceholderRole.TITLE, title, 4400),
                (PlaceholderRole.TEXT, left, 2800),
                (PlaceholderRole.TEXT, right, 2800)),

            DefaultLayout(5, "Content with Picture", "cust",
                (PlaceholderRole.TITLE, title, 4400),
                (PlaceholderRole.TEXT, left, 2800),
                (PlaceholderRole.PICTURE, right, DEFAULT_FONT_SIZE)),

            DefaultLayout(6, "Table", "tbl",
                (PlaceholderRole.TITLE, title, 4400),
                (PlaceholderRole.TABLE, body, DEFAULT_FONT_SIZE)),
        ];

        var families = layouts.Select((layout, index) => new LayoutFamily($"L{index + 1}", layout.Name, [new LayoutVariant(null, layout)])).ToList();
        return new PresentationTemplate(DEFAULT_SLIDE_WIDTH, DEFAULT_SLIDE_HEIGHT, families, [], []);
    }

    private static TemplateLayout DefaultLayout(int number, string name, string layoutType, params (PlaceholderRole Role, SlotBounds Bounds, int FontSize)[] slots)
    {
        var templateSlots = slots.Select(slot => new TemplateSlot(slot.Role, 0, slot.Bounds, DefaultPlaceholderTypeOf(slot.Role), null, CapacityOf(slot.Role, slot.Bounds, slot.FontSize, InsetsOf(null))));
        return new TemplateLayout(name, new Uri($"/ppt/slideLayouts/slideLayout{number}.xml", UriKind.Relative), layoutType, InReadingOrder(templateSlots, DEFAULT_SLIDE_HEIGHT));
    }

    private static string DefaultPlaceholderTypeOf(PlaceholderRole role) => role switch
    {
        PlaceholderRole.TITLE => "title",
        PlaceholderRole.SUBTITLE => "subTitle",
        PlaceholderRole.PICTURE => "pic",
        PlaceholderRole.TABLE => "tbl",

        _ => "body",
    };
}