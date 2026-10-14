using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal static partial class PresentationWriter
{
    /// <summary>
    /// Where the values of the imprint start, measured from the left edge of the text, in EMU.
    /// </summary>
    /// <remarks>
    /// The labels hang out to the left by as much, so the tab after a label moves on to the value,
    /// and the values line up in a column. The width is that of the imprint the lines come from.
    /// </remarks>
    private const int IMPRINT_VALUE_INDENT = 1_792_288;

    /// <summary>
    /// The sample name of the institute, which needs no translation.
    /// </summary>
    private const string INSTITUTE_PLACEHOLDER = "Lorem ipsum dolor sit amet";

    /// <summary>
    /// The texts of the imprint: those the model wrote in the language of the presentation, or the German ones.
    /// </summary>
    private static ImprintTexts ImprintTextsOf(SlideDeckSpec deck) => deck.Imprint ?? ImprintTexts.GERMAN;

    /// <summary>
    /// Chooses the layout family the imprint is written on.
    /// </summary>
    /// <remarks>
    /// The plainest family with a title and a text fits best: exactly one of each, and nothing which
    /// would stay empty in plain view, such as a picture. Labels may be there, since a label without
    /// content is left out anyway. A template without such a family gets the first family with a
    /// title and a text, and a template without even that gets no imprint.
    /// </remarks>
    /// <returns>The family, or null when the template has no layout for the imprint.</returns>
    private static LayoutFamily? ImprintFamilyOf(PresentationTemplate template) =>
        template.Families.FirstOrDefault(family =>
            family.Slots.Count(slot => slot.Role is PlaceholderRole.TITLE) == 1 &&
            family.Slots.Count(slot => slot.Role is PlaceholderRole.TEXT) == 1 &&
            !family.Slots.Any(slot => slot.Role is PlaceholderRole.SUBTITLE or PlaceholderRole.PICTURE or PlaceholderRole.TABLE)) ??
        template.Families.FirstOrDefault(family =>
            family.Slots.Any(slot => slot.Role is PlaceholderRole.TITLE) &&
            family.Slots.Any(slot => slot.Role is PlaceholderRole.TEXT));

    /// <summary>
    /// The shape tree of the imprint: its title, its lines in the first text slot, footer and slide number.
    /// </summary>
    /// <param name="layout">The layout the imprint is based on, which has a title and a text slot.</param>
    /// <param name="layoutElement">The same layout in the document being written.</param>
    /// <param name="deck">The slide deck, whose title is the topic and which holds the texts of the imprint.</param>
    /// <param name="footerText">The footer text; empty for no footer.</param>
    private static P.ShapeTree ImprintShapeTreeOf(TemplateLayout layout, P.SlideLayout? layoutElement, SlideDeckSpec deck, string footerText)
    {
        var placeholders = LayoutPlaceholders.Of(layoutElement);
        var title = layout.Slots.First(slot => slot.Role is PlaceholderRole.TITLE);
        var text = layout.Slots.First(slot => slot.Role is PlaceholderRole.TEXT);
        var texts = ImprintTextsOf(deck);

        var shapeTree = NewShapeTree();
        shapeTree.Append(TextShape(2, "Title 2", placeholders.For(title), ParagraphsOf(texts.Title), false));
        shapeTree.Append(TextShape(3, "Text 3", placeholders.For(text), ImprintParagraphsOf(deck.Title, texts), true));
        AppendFooterAndSlideNumber(shapeTree, placeholders, footerText, 4);
        return shapeTree;
    }

    /// <summary>
    /// The lines of the imprint.
    /// </summary>
    /// <remarks>
    /// Only the topic is filled in. The other values are placeholders, which the author replaces in
    /// PowerPoint; the date says the format it expects.
    /// </remarks>
    private static IEnumerable<A.Paragraph> ImprintParagraphsOf(string topic, ImprintTexts texts) =>
    [
        ImprintParagraph(texts.TopicLabel, topic.Trim(), true),
        ImprintParagraph(texts.DateLabel, texts.DatePlaceholder, false),
        ImprintParagraph(texts.AuthorLabel, texts.AuthorPlaceholder, false),
        ImprintParagraph(texts.InstituteLabel, INSTITUTE_PLACEHOLDER, false),
        ImprintParagraph(texts.PictureCreditsLabel, texts.PictureCreditsPlaceholder, false),
    ];

    /// <summary>
    /// One line of the imprint: the label, a tab, and the value in the column of all values.
    /// </summary>
    /// <remarks>
    /// The imprint is a list of facts rather than of points, so its lines get no bullets. A line
    /// break in the value continues it in its column.
    /// </remarks>
    private static A.Paragraph ImprintParagraph(string label, string value, bool isBold)
    {
        var paragraph = new A.Paragraph(
            new A.ParagraphProperties(new A.NoBullet()) { LeftMargin = IMPRINT_VALUE_INDENT, Indent = -IMPRINT_VALUE_INDENT },
            ImprintRun($"{label} \t", false));

        var lines = value.ReplaceLineEndings("\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
                paragraph.Append(new A.Break());

            if (lines[index].Length > 0)
                paragraph.Append(ImprintRun(lines[index], isBold));
        }

        return paragraph;
    }

    /// <summary>
    /// A run of the imprint. Its texts come from the model, so they lose what XML cannot carry.
    /// </summary>
    private static A.Run ImprintRun(string text, bool isBold) => isBold
        ? new A.Run(new A.RunProperties { Bold = true }, new A.Text(XmlSafe(text)))
        : new A.Run(new A.Text(XmlSafe(text)));
}