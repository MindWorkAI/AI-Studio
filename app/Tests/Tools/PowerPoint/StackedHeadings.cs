using AIStudio.Tools.PowerPoint;

using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// A layout with a heading above a text, and a slide on it which stacks three headings with their
/// texts in the left half, beside a picture with its source, as authors of corporate templates do.
/// </summary>
/// <remarks>
/// Only the first heading and the first text are placeholders. The others are text boxes copied
/// from them: PowerPoint keeps the look of a copied placeholder, but not its link to the layout.
/// Each heading sits closer to the text above it than to its own, as on the slides this was taken
/// from. At 20 pt, the heading of the layout holds 1 line of 22 characters and its text 10 lines of
/// 50 characters; each text of the slide holds 3 lines of 25 characters.
/// </remarks>
internal static class StackedHeadings
{
    public static readonly SlotBounds HEADING = new(838_200, 1_400_000, 3_000_000, 300_000);

    /// <summary>
    /// The text of the layout, sized for 10.5 lines and 50.5 characters at 20 pt, plus the default insets.
    /// </summary>
    public static readonly SlotBounds TEXT = new(838_200, 1_825_625, 6_413_500 + 2 * 91_440, 3_200_400 + 2 * 45_720);

    public static readonly SlotBounds FIRST_TEXT = new(838_200, 1_825_625, 3_400_000, 1_000_000);
    public static readonly SlotBounds SECOND_HEADING = new(838_200, 2_850_000, 2_000_000, 300_000);
    public static readonly SlotBounds SECOND_TEXT = new(838_200, 3_275_625, 3_400_000, 1_000_000);
    public static readonly SlotBounds THIRD_HEADING = new(838_200, 4_300_000, 2_500_000, 300_000);
    public static readonly SlotBounds THIRD_TEXT = new(838_200, 4_725_625, 3_400_000, 1_000_000);
    public static readonly SlotBounds PICTURE = new(6_200_000, 1_825_625, 5_150_000, 3_500_000);
    public static readonly SlotBounds PICTURE_SOURCE = new(6_200_000, 5_375_625, 5_150_000, 300_000);

    /// <summary>
    /// The color of the headings the author copied.
    /// </summary>
    public const string HEADING_FILL = "4472C4";

    /// <summary>
    /// The layout with a heading above a text.
    /// </summary>
    /// <param name="name">The name of the layout, e.g., "B Content" for a color variant.</param>
    public static TestLayout Layout(string name = "Content with heading") => new(name, TestPlaceholder.Title(), TestPlaceholder.Label(17, HEADING, 2000), TestPlaceholder.Text(1, TEXT, 2000));

    /// <summary>
    /// The slide stacking three headings with their texts: title, heading and text are the
    /// placeholders 2 to 4, the copies of heading and text the shapes 5 to 8, the picture 9 and its
    /// source 10.
    /// </summary>
    /// <param name="layout">The position of the layout in the template, starting at 0.</param>
    public static TestSlide Slide(int layout = 0) => new(layout,
        (TestPlaceholder.Title(), "Missions"),
        (new TestPlaceholder(P.PlaceholderValues.Body, 17), "Lorem ipsum"),
        (TestPlaceholder.Text(1, FIRST_TEXT), "Ut wisi enim"))
    {
        Elements =
        [
            TestElement.TextBox(SECOND_HEADING, "Lorem", HEADING_FILL),
            TestElement.TextBox(SECOND_TEXT, "Duis autem"),
            TestElement.TextBox(THIRD_HEADING, "Nam liber", HEADING_FILL),
            TestElement.TextBox(THIRD_TEXT, "Consectetuer"),
            TestElement.Picture(PICTURE, 1, 2, 3),
            TestElement.TextBox(PICTURE_SOURCE, "Bildquelle hier angeben"),
        ],
    };
}