using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tests.Tools.Word;

/// <summary>
/// Builds a small Word template in memory, so that no test depends on the template file of an organization.
/// </summary>
/// <remarks>
/// The template looks like one a German Word saved: its styles carry German IDs, under the English
/// names Word gives its built-in styles. It has no second heading level. Its body holds what a
/// corporate template brings along: a cover page in a section with a header of its own, sample text
/// with a comment and a footnote, a picture, and a link. The last section has the header which the
/// document keeps. TestWordTemplateOptions tells what else the template holds.
/// </remarks>
internal static class TestWordTemplate
{
    public const string NORMAL_STYLE_ID = "Standard";
    public const string HEADING_1_STYLE_ID = "berschrift1";
    public const string HYPERLINK_STYLE_ID = "Link";

    public const string HEADER_TEXT = "Header of the organization";
    public const string COVER_HEADER_TEXT = "Header of the cover page";
    public const string FOOTER_TEXT = "Address of the organization";

    /// <summary>
    /// The top margin of the pages, 2.2 cm, in twentieths of a point.
    /// </summary>
    public const int TOP_MARGIN = 1247;

    /// <summary>
    /// The top margin of a cover page, 9.8 cm, which leaves room for a big picture in its header.
    /// </summary>
    public const int COVER_TOP_MARGIN = 5568;

    private const long PICTURE_SIZE = 952_500;

    /// <summary>
    /// Writes the template.
    /// </summary>
    /// <param name="options">What else the template holds; null for nothing.</param>
    /// <returns>The file, positioned at its start.</returns>
    public static MemoryStream Create(TestWordTemplateOptions? options = null)
    {
        options ??= new TestWordTemplateOptions();
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, options.AsTemplate ? WordprocessingDocumentType.Template : WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                new W.Style
                {
                    Type = W.StyleValues.Paragraph,
                    StyleId = NORMAL_STYLE_ID,
                    Default = true,
                    StyleName = new W.StyleName { Val = "Normal" },
                    PrimaryStyle = new W.PrimaryStyle(),
                },
                new W.Style
                {
                    Type = W.StyleValues.Paragraph,
                    StyleId = HEADING_1_STYLE_ID,
                    StyleName = new W.StyleName { Val = "heading 1" },
                    BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID },
                    PrimaryStyle = new W.PrimaryStyle(),
                    StyleParagraphProperties = new W.StyleParagraphProperties { OutlineLevel = new W.OutlineLevel { Val = 0 } },
                },
                new W.Style
                {
                    Type = W.StyleValues.Character,
                    StyleId = HYPERLINK_STYLE_ID,
                    StyleName = new W.StyleName { Val = "Hyperlink" },
                });

            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text(HEADER_TEXT))));

            var coverHeaderPart = mainPart.AddNewPart<HeaderPart>();
            coverHeaderPart.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text(COVER_HEADER_TEXT))));

            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var picture = new MemoryStream([137, 80, 78, 71]))
                imagePart.FeedData(picture);

            var commentsPart = mainPart.AddNewPart<WordprocessingCommentsPart>();
            commentsPart.Comments = new W.Comments(new W.Comment(new W.Paragraph(new W.Run(new W.Text("Please fill in the title.")))) { Id = "0", Author = "IT" });

            var footnotesPart = mainPart.AddNewPart<FootnotesPart>();
            footnotesPart.Footnotes = new W.Footnotes(
                new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                new W.Footnote(new W.Paragraph(new W.Run(new W.Text("A sample note.")))) { Id = 1 });

            var link = mainPart.AddHyperlinkRelationship(new Uri("https://example.org/sample"), true);

            mainPart.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Cover page"))),
                new W.Paragraph(new W.ParagraphProperties
                {
                    SectionProperties = options.CoverPage
                        ? CoverSectionOf(mainPart, headerPart, coverHeaderPart)
                        : new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(coverHeaderPart) }),
                }),
                new W.Paragraph(
                    new W.CommentRangeStart { Id = "0" },
                    new W.Run(new W.Text("Replace this text with yours.")),
                    new W.CommentRangeEnd { Id = "0" },
                    new W.Run(new W.CommentReference { Id = "0" }),
                    new W.Run(new W.FootnoteReference { Id = 1 })),
                new W.Paragraph(new W.Run(PictureOf(mainPart.GetIdOfPart(imagePart)))),
                new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("A sample link"))) { Id = link.Id }),
                options.CoverPage
                    ? new W.SectionProperties(PageSize(), PageMarginOf(TOP_MARGIN))
                    : new W.SectionProperties(
                        new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
                        PageSize())));
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// The first section of a corporate template, which states the headers and footers for every
    /// section after it, and a header of its own for its first page.
    /// </summary>
    private static W.SectionProperties CoverSectionOf(MainDocumentPart mainPart, HeaderPart headerPart, HeaderPart coverHeaderPart)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var logoPart = footerPart.AddImagePart(ImagePartType.Png);
        using (var logo = new MemoryStream([137, 80, 78, 71]))
            logoPart.FeedData(logo);

        footerPart.Footer = new W.Footer(new W.Paragraph(new W.Run(PictureOf(footerPart.GetIdOfPart(logoPart))), new W.Run(new W.Text(FOOTER_TEXT))));
        return new W.SectionProperties(
            new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
            new W.HeaderReference { Type = W.HeaderFooterValues.First, Id = mainPart.GetIdOfPart(coverHeaderPart) },
            new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) },
            PageSize(),
            PageMarginOf(COVER_TOP_MARGIN),
            new W.TitlePage());
    }

    /// <summary>
    /// A page of DIN A4.
    /// </summary>
    private static W.PageSize PageSize() => new() { Width = 11_906U, Height = 16_838U };

    private static W.PageMargin PageMarginOf(int top) => new() { Top = top, Right = 851U, Bottom = 709, Left = 851U, Header = 709U, Footer = 709U, Gutter = 0U };

    /// <summary>
    /// A picture in the line of text, the way Word inserts one.
    /// </summary>
    private static W.Drawing PictureOf(string relationshipId) => new(
        new DW.Inline(
            new DW.Extent { Cx = PICTURE_SIZE, Cy = PICTURE_SIZE },
            new DW.DocProperties { Id = 1U, Name = "Logo" },
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relationshipId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = PICTURE_SIZE, Cy = PICTURE_SIZE }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                }))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U,
        });
}