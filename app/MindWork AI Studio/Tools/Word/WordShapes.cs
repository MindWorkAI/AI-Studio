using System.Globalization;

using DocumentFormat.OpenXml;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Takes over the boxes, pictures, and lines which a template places around the text of a page.
/// </summary>
/// <remarks>
/// Templates float shapes around their text: pictures with captions in a wide margin, a box for
/// the title above the text, or a picture behind the whole page. These belong to the design and
/// stay. Shapes within the text, such as a sample table in a text box or a chart the text flows
/// around, belong to the sample text and go with it. Which is which follows from where a shape
/// lies, never from what it shows. A shape belongs to the design when
///
/// - it lies outside the area in which the text of its page runs, i.e., in a margin, or above
///   where the text of the page starts, or
/// - it lies behind the text, and the text does not flow around it.
///
/// Each shape is anchored at a paragraph of the template, which goes with the body. So the shapes
/// of a page move to the first paragraph of the same page of the document, and their positions are
/// stated in relation to the page. What the template places in relation to its paragraph moves by
/// as much as WordLayoutEstimate finds the paragraph below the top of the page, which is exact
/// for the first paragraph of a page only. A shape whose position Word 2007 could not read, e.g.,
/// one in percent of the page, goes as well.
///
/// A shape keeps its markup for Word 2010 and later only, without the one for older versions,
/// which the file states as an alternative for some kinds of shapes, e.g., text boxes.
/// </remarks>
internal static class WordShapes
{
    /// <summary>
    /// Returns copies of the shapes of a page of the template which belong to its design.
    /// </summary>
    /// <param name="page">The paragraphs and tables of the page, in the order of the template.</param>
    /// <param name="area">The page and its text area.</param>
    /// <param name="textTop">Where the text of the page starts, in twentieths of a point from the top edge of the page.</param>
    /// <param name="estimate">Estimates where the paragraphs of the page start.</param>
    /// <returns>A run for each shape, with its position stated in relation to the page.</returns>
    public static List<W.Run> DesignOf(IReadOnlyList<OpenXmlElement> page, WordTextArea area, int textTop, WordLayoutEstimate estimate)
    {
        var shapes = new List<W.Run>();
        var blockTop = area.Top;
        foreach (var block in page)
        {
            foreach (var anchor in block.OwnDescendants<DW.Anchor>())
                if (DesignShapeOf(anchor, area, textTop, blockTop) is { } shape)
                    shapes.Add(new W.Run(new W.Drawing(shape)));

            blockTop += estimate.HeightOf(block, area.ColumnWidth);
        }

        return shapes;
    }

    /// <summary>
    /// Returns the text box among the shapes of a page which holds the title of the document, e.g.,
    /// a coloured box on a picture at the top of the first page.
    /// </summary>
    /// <param name="shapes">The shapes, see DesignOf.</param>
    /// <param name="styles">The styles of the template.</param>
    /// <returns>The content of the first text box with a paragraph in the style of a title; null when there is none.</returns>
    public static W.TextBoxContent? TitleBoxOf(IEnumerable<W.Run> shapes, WordStyles styles) => styles.Title is { } titleStyleId
        ? shapes.SelectMany(shape => shape.Descendants<W.TextBoxContent>()).FirstOrDefault(box => ParagraphsOf(box, titleStyleId).Any())
        : null;

    /// <summary>
    /// Writes the title into a title box, in place of the sample text of the template.
    /// </summary>
    /// <remarks>
    /// The first paragraph in the style of a title takes the title, with the look of its first run,
    /// e.g., white letters in capitals. Further ones go, since templates show a title of two lines
    /// as two paragraphs. A subtitle stays empty, as the answer has none.
    /// </remarks>
    /// <param name="box">The content of the title box, see TitleBoxOf.</param>
    /// <param name="title">The title, e.g., the text of the heading the answer starts with.</param>
    /// <param name="styles">The styles of the template.</param>
    public static void FillTitleBox(W.TextBoxContent box, string title, WordStyles styles)
    {
        var titles = ParagraphsOf(box, styles.Title).ToList();
        if (titles.Count is 0)
            return;

        var look = (W.RunProperties?)titles[0].Descendants<W.RunProperties>().FirstOrDefault()?.CloneNode(true);
        foreach (var paragraph in titles.Skip(1))
            paragraph.Remove();

        foreach (var paragraph in ParagraphsOf(box, styles.Subtitle).Prepend(titles[0]).ToList())
            foreach (var content in paragraph.ChildElements.Where(element => element is not W.ParagraphProperties).ToList())
                content.Remove();

        var run = new W.Run(new W.Text(title) { Space = SpaceProcessingModeValues.Preserve });
        if (look is not null)
            run.PrependChild(look);

        titles[0].Append(run);
    }

    /// <summary>
    /// Returns how far up from the bottom edge of the page the shapes of a footer reach across the
    /// text, e.g., an address in a text box.
    /// </summary>
    /// <remarks>
    /// Word keeps the text clear of the text of a footer, but not of the shapes floating in it. Only
    /// shapes in the lower half of the page count, as a footer places them at the foot of the page,
    /// and only those across the columns of the text: a logo in a wide margin beside them is in
    /// nobody's way. The paragraphs of the footer end where the footer distance of the section
    /// says, so a shape placed in relation to one of them lies as far up as WordLayoutEstimate finds
    /// the paragraph.
    /// </remarks>
    /// <param name="footer">The footer.</param>
    /// <param name="area">The page and the text area of the section which shows the footer.</param>
    /// <param name="footerDistance">The distance between the bottom edge of the page and the footer, in twentieths of a point.</param>
    /// <param name="estimate">Estimates the height of the paragraphs of the footer.</param>
    /// <returns>The distance between the bottom edge of the page and the top of the highest of these
    /// shapes, together with the space it keeps from text, in twentieths of a point; 0 when there is none.</returns>
    public static int FootOf(W.Footer footer, WordTextArea area, int footerDistance, WordLayoutEstimate estimate)
    {
        var blocks = footer.ChildElements.Where(element => element is W.Paragraph or W.Table).ToList();
        var blockTop = area.PageHeight - footerDistance - blocks.Sum(block => estimate.HeightOf(block, area.ColumnWidth));
        var foot = 0L;
        foreach (var block in blocks)
        {
            foreach (var anchor in block.OwnDescendants<DW.Anchor>())
                if (BoundsOf(anchor, area, blockTop) is { } bounds && bounds.Top >= Emu(area.PageHeight) / 2 && CrossesColumn(bounds.Left, bounds.Width, area))
                    foot = Math.Max(foot, Emu(area.PageHeight) - bounds.Top + (anchor.DistanceFromTop?.Value ?? 0));

            blockTop += estimate.HeightOf(block, area.ColumnWidth);
        }

        return (int)((foot + WordTextArea.EMU_PER_TWIP - 1) / WordTextArea.EMU_PER_TWIP);
    }

    /// <summary>
    /// Returns the paragraphs of a text box in the given style.
    /// </summary>
    private static IEnumerable<W.Paragraph> ParagraphsOf(W.TextBoxContent box, string? styleId) => styleId is null
        ? Enumerable.Empty<W.Paragraph>()
        : box.Descendants<W.Paragraph>().Where(paragraph => paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value == styleId);

    /// <summary>
    /// Returns a copy of a shape with its position in relation to the page, when it belongs to the design.
    /// </summary>
    /// <param name="anchor">The shape, as the template anchors it.</param>
    /// <param name="area">The page and its text area.</param>
    /// <param name="textTop">Where the text of the page starts, in twentieths of a point.</param>
    /// <param name="paragraphTop">Where the paragraph which anchors the shape starts, in twentieths of a point.</param>
    /// <returns>The copy; null for a shape of the sample text, or one whose position Word 2007 could not read.</returns>
    private static DW.Anchor? DesignShapeOf(DW.Anchor anchor, WordTextArea area, int textTop, int paragraphTop)
    {
        if (BoundsOf(anchor, area, paragraphTop) is not { } bounds)
            return null;

        var (x, y, width, height) = bounds;
        var isBackground = anchor.BehindDoc?.Value is true && anchor.GetFirstChild<DW.WrapNone>() is not null;
        var isBesideText = !CrossesColumn(x, width, area)
                           || y + height <= Emu(textTop)
                           || y >= Emu(area.PageHeight - area.Bottom);

        if (!isBackground && !isBesideText)
            return null;

        var shape = (DW.Anchor)anchor.CloneNode(true);

        // A position Word 2007 cannot read comes with one it can read, as an alternative:
        foreach (var alternative in shape.Elements<AlternateContent>().Where(alternative => alternative.Descendants<DW.HorizontalPosition>().Any() || alternative.Descendants<DW.VerticalPosition>().Any()).ToList())
            alternative.Remove();

        shape.SimplePos = false;
        shape.HorizontalPosition = new DW.HorizontalPosition(new DW.PositionOffset(x.ToString(CultureInfo.InvariantCulture))) { RelativeFrom = DW.HorizontalRelativePositionValues.Page };
        shape.VerticalPosition = new DW.VerticalPosition(new DW.PositionOffset(y.ToString(CultureInfo.InvariantCulture))) { RelativeFrom = DW.VerticalRelativePositionValues.Page };
        return shape;
    }

    /// <summary>
    /// Returns where a shape lies on the page.
    /// </summary>
    /// <param name="anchor">The shape.</param>
    /// <param name="area">The page and its text area.</param>
    /// <param name="paragraphTop">Where the paragraph which anchors the shape starts, in twentieths of a point.</param>
    /// <returns>The left and top edges and the size, in EMU; null when the shape states no position Word 2007 could read.</returns>
    private static (long Left, long Top, long Width, long Height)? BoundsOf(DW.Anchor anchor, WordTextArea area, int paragraphTop)
    {
        var extent = anchor.GetFirstChild<DW.Extent>();
        var width = extent?.Cx?.Value ?? 0;
        var height = extent?.Cy?.Value ?? 0;
        var isPlacedOnThePage = anchor.SimplePos?.Value is true;
        var left = isPlacedOnThePage ? anchor.SimplePosition?.X?.Value : HorizontalPositionOf(anchor, width, area);
        var top = isPlacedOnThePage ? anchor.SimplePosition?.Y?.Value : VerticalPositionOf(anchor, height, area, paragraphTop);
        return left is { } x && top is { } y ? (x, y, width, height) : null;
    }

    /// <summary>
    /// Determines whether a shape lies across the columns of the text, at whatever height, rather than in a margin beside them.
    /// </summary>
    private static bool CrossesColumn(long left, long width, WordTextArea area) => left + width > Emu(area.Left + area.Gutter) && left < Emu(area.PageWidth - area.Right);

    /// <summary>
    /// Returns how far the left edge of a shape is from the left edge of the page, in EMU.
    /// </summary>
    /// <remarks>
    /// The first page of a document is a right-hand one, so its inside margin is the left one.
    /// </remarks>
    /// <returns>The distance; null when the shape states no position Word 2007 could read.</returns>
    private static long? HorizontalPositionOf(DW.Anchor anchor, long width, WordTextArea area)
    {
        var position = PositionsOf<DW.HorizontalPosition>(anchor).FirstOrDefault(candidate => candidate.GetFirstChild<DW.PositionOffset>() is not null || candidate.GetFirstChild<DW.HorizontalAlignment>() is not null);
        if (position is null)
            return null;

        var (start, size) = position.RelativeFrom?.InnerText switch
        {
            "page" => (0, area.PageWidth),
            "leftMargin" or "insideMargin" => (0, area.Left + area.Gutter),
            "rightMargin" or "outsideMargin" => (area.PageWidth - area.Right, area.Right),
            "column" or "character" => (area.Left + area.Gutter, area.ColumnWidth),

            _ => (area.Left + area.Gutter, area.PageWidth - area.Left - area.Gutter - area.Right),
        };

        return PlaceOf(Emu(start), Emu(size), width, position.GetFirstChild<DW.PositionOffset>()?.Text, position.GetFirstChild<DW.HorizontalAlignment>()?.Text);
    }

    /// <summary>
    /// Returns how far the top edge of a shape is from the top edge of the page, in EMU.
    /// </summary>
    /// <returns>The distance; null when the shape states no position Word 2007 could read.</returns>
    private static long? VerticalPositionOf(DW.Anchor anchor, long height, WordTextArea area, int paragraphTop)
    {
        var position = PositionsOf<DW.VerticalPosition>(anchor).FirstOrDefault(candidate => candidate.GetFirstChild<DW.PositionOffset>() is not null || candidate.GetFirstChild<DW.VerticalAlignment>() is not null);
        if (position is null)
            return null;

        var (start, size) = position.RelativeFrom?.InnerText switch
        {
            "page" => (0, area.PageHeight),
            "topMargin" or "insideMargin" => (0, area.Top),
            "bottomMargin" or "outsideMargin" => (area.PageHeight - area.Bottom, area.Bottom),
            "paragraph" or "line" => (paragraphTop, 0),

            _ => (area.Top, area.PageHeight - area.Top - area.Bottom),
        };

        return PlaceOf(Emu(start), Emu(size), height, position.GetFirstChild<DW.PositionOffset>()?.Text, position.GetFirstChild<DW.VerticalAlignment>()?.Text);
    }

    /// <summary>
    /// Returns the positions of a kind which a shape states, its own first, then those it states as alternatives.
    /// </summary>
    private static IEnumerable<T> PositionsOf<T>(DW.Anchor anchor) where T : OpenXmlElement => anchor.Elements<T>().Concat(anchor.Elements<AlternateContent>().SelectMany(alternative => alternative.Descendants<T>()));

    /// <summary>
    /// Returns where a shape starts within the area it is placed in.
    /// </summary>
    /// <param name="start">Where the area starts, in EMU.</param>
    /// <param name="size">The size of the area, in EMU.</param>
    /// <param name="extent">The size of the shape, in EMU.</param>
    /// <param name="offset">How far the shape is from the start of the area, in EMU; null when it is aligned.</param>
    /// <param name="alignment">How the shape is aligned within the area; null when it is placed at an offset.</param>
    /// <returns>Where the shape starts, in EMU; null when it is neither placed nor aligned.</returns>
    private static long? PlaceOf(long start, long size, long extent, string? offset, string? alignment)
    {
        if (long.TryParse(offset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var distance))
            return start + distance;

        return alignment switch
        {
            "left" or "top" or "inside" => start,
            "center" => start + (size - extent) / 2,
            "right" or "bottom" or "outside" => start + size - extent,

            _ => null,
        };
    }

    private static long Emu(int twips) => twips * WordTextArea.EMU_PER_TWIP;
}