using System.Globalization;

using AIStudio.Tools.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

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
    public const string LIST_PARAGRAPH_STYLE_ID = "Listenabsatz";
    public const string LIST_NUMBER_STYLE_ID = "Listennummer";

    /// <summary>
    /// The numbering instance of the dashes which "List Paragraph" of the template brings along.
    /// </summary>
    public const int BULLET_INSTANCE_ID = 7;

    /// <summary>
    /// The numbering instance of the numbers which "List Number" of the template brings along.
    /// </summary>
    public const int NUMBER_INSTANCE_ID = 8;

    /// <summary>
    /// The definition of the numbers which "List Number" of the template brings along.
    /// </summary>
    public const int NUMBER_DEFINITION_ID = 4;

    /// <summary>
    /// How far each level of a list of the template moves in, half a centimeter.
    /// </summary>
    public const int LIST_INDENTATION = 284;

    /// <summary>
    /// The indentation of "List Paragraph" of the template, a quarter of a centimeter, with which it
    /// pulls its dashes further out than its numbering does.
    /// </summary>
    public const int LIST_STYLE_INDENTATION = 153;

    private const int BULLET_DEFINITION_ID = 3;

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

    /// <summary>
    /// The left and right margins of the pages, 1.5 cm.
    /// </summary>
    public const int SIDE_MARGIN = 851;

    /// <summary>
    /// A margin wide enough for a column of pictures next to the text, 7.65 cm.
    /// </summary>
    public const int SIDEBAR_MARGIN = 4338;

    public const string MARGIN_PICTURE = "Picture in the margin";
    public const string MARGIN_CAPTION = "Caption in the margin";
    public const string BACKGROUND_PICTURE = "Picture behind the page";
    public const string SAMPLE_PICTURE = "Sample picture in the text";
    public const string NEXT_PAGE_PICTURE = "Picture in the margin of the next page";
    public const string CAPTION_TEXT = "A picture of the organization.";

    public const string TITLE_STYLE_ID = "Titel";
    public const string SUBTITLE_STYLE_ID = "Untertitel";
    public const string TITLE_BOX = "Title box";
    public const string SAMPLE_TITLE = "24 PT TITLE";
    public const string SAMPLE_TITLE_SECOND_LINE = "OF TWO LINES AT MOST";
    public const string SAMPLE_SUBTITLE = "A subtitle of one line.";

    /// <summary>
    /// The color of the letters of the sample title: white, on a box of the color of the organization.
    /// </summary>
    public const string TITLE_COLOR = "FFFFFF";

    /// <summary>
    /// How far the address in the footer reaches up from the bottom edge of the page, 3.35 cm,
    /// while the bottom margin of the pages is 1.25 cm.
    /// </summary>
    public const int FOOTER_CLEARANCE = 1898;

    /// <summary>
    /// The bottom margin of the pages, 1.25 cm.
    /// </summary>
    public const int BOTTOM_MARGIN = 709;

    public const string SAMPLE_CHAPTER = "Sample chapter";
    public const string APPENDIX_TEXT = "Remove this page afterwards.";

    /// <summary>
    /// The bottom margin of the section of the second chapter, 3 cm, which keeps the text further
    /// away from the footer than the one of the first chapter, which shares the section of the cover page.
    /// </summary>
    public const int CHAPTER_BOTTOM_MARGIN = 1701;

    /// <summary>
    /// How far the picture and the caption in the margin of the first page are from the left edge of the page, in EMU: ten points.
    /// </summary>
    public const long MARGIN_PICTURE_LEFT = 127_000;

    private const int PAGE_WIDTH = 11_906;
    private const int PAGE_HEIGHT = 16_838;
    private const string WORDPROCESSING_SHAPE_NAMESPACE = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

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
            var styles = new List<W.Style>
            {
                new()
                {
                    Type = W.StyleValues.Paragraph,
                    StyleId = NORMAL_STYLE_ID,
                    Default = true,
                    StyleName = new W.StyleName { Val = "Normal" },
                    PrimaryStyle = new W.PrimaryStyle(),
                    StyleParagraphProperties = new W.StyleParagraphProperties { SpacingBetweenLines = new W.SpacingBetweenLines { After = options.NormalSpacingAfter.ToString() } },
                },
                new()
                {
                    Type = W.StyleValues.Paragraph,
                    StyleId = HEADING_1_STYLE_ID,
                    StyleName = new W.StyleName { Val = "heading 1" },
                    BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID },
                    PrimaryStyle = new W.PrimaryStyle(),
                    StyleParagraphProperties = new W.StyleParagraphProperties { KeepNext = new W.KeepNext(), OutlineLevel = new W.OutlineLevel { Val = 0 } },
                },
                new()
                {
                    Type = W.StyleValues.Character,
                    StyleId = HYPERLINK_STYLE_ID,
                    StyleName = new W.StyleName { Val = "Hyperlink" },
                },
            };

            if (options.ListNumbering)
            {
                styles.Add(ListStyleOf(LIST_PARAGRAPH_STYLE_ID, "List Paragraph", BULLET_INSTANCE_ID, LIST_STYLE_INDENTATION));
                styles.Add(ListStyleOf(LIST_NUMBER_STYLE_ID, "List Number", NUMBER_INSTANCE_ID));
                mainPart.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                    ListDefinitionOf(BULLET_DEFINITION_ID, W.NumberFormatValues.Bullet, _ => "-"),
                    ListDefinitionOf(NUMBER_DEFINITION_ID, W.NumberFormatValues.Decimal, level => $"%{level + 1})"),
                    new W.NumberingInstance(new W.AbstractNumId { Val = BULLET_DEFINITION_ID }) { NumberID = BULLET_INSTANCE_ID },
                    new W.NumberingInstance(new W.AbstractNumId { Val = NUMBER_DEFINITION_ID }) { NumberID = NUMBER_INSTANCE_ID });
            }

            if (options.TitleBox)
            {
                styles.Add(new W.Style { Type = W.StyleValues.Paragraph, StyleId = TITLE_STYLE_ID, StyleName = new W.StyleName { Val = "Title" }, BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID } });
                styles.Add(new W.Style { Type = W.StyleValues.Paragraph, StyleId = SUBTITLE_STYLE_ID, StyleName = new W.StyleName { Val = "Subtitle" }, BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID } });
            }

            mainPart.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(styles);

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

            var (firstPageLeft, otherPagesRight) = options.SidebarLayout ? (SIDEBAR_MARGIN, SIDEBAR_MARGIN) : (SIDE_MARGIN, SIDE_MARGIN);
            var body = new W.Body();
            for (var index = 0; index < options.LeadingEmptyParagraphs; index++)
                body.Append(new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = HEADING_1_STYLE_ID } }));

            var coverParagraph = options.EmptyCoverPage ? new W.Paragraph() : new W.Paragraph(new W.Run(new W.Text("Cover page")));
            var sampleParagraph = new W.Paragraph(
                new W.CommentRangeStart { Id = "0" },
                new W.Run(new W.Text("Replace this text with yours.")),
                new W.CommentRangeEnd { Id = "0" },
                new W.Run(new W.CommentReference { Id = "0" }),
                new W.Run(new W.FootnoteReference { Id = 1 }));

            if (options.Shapes)
            {
                coverParagraph.Append(FirstPageShapesOf(mainPart, firstPageLeft));
                sampleParagraph.Append(NextPageShapesOf(mainPart, otherPagesRight));
            }

            if (options.TitleBox)
                coverParagraph.Append(TitleBox());

            body.Append(
                coverParagraph,
                new W.Paragraph(new W.ParagraphProperties
                {
                    SectionProperties = options.CoverPage
                        ? CoverSectionOf(mainPart, headerPart, coverHeaderPart, firstPageLeft, options.FooterShapes)
                        : new W.SectionProperties(
                            new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(coverHeaderPart) },
                            PageSize(),
                            PageMarginOf(TOP_MARGIN, left: firstPageLeft)),
                }));

            if (options.TableOfContents)
            {
                body.Append(TableOfContents());
                body.Append(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
            }

            if (options.TableOfContents || options.AppendixPage)
                body.Append(new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = HEADING_1_STYLE_ID } }, new W.Run(new W.Text(SAMPLE_CHAPTER))));

            body.Append(
                sampleParagraph,
                new W.Paragraph(new W.Run(PictureOf(mainPart.GetIdOfPart(imagePart)))),
                new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("A sample link"))) { Id = link.Id }));

            var mainSection = options.CoverPage
                ? new W.SectionProperties(PageSize(), PageMarginOf(TOP_MARGIN, right: otherPagesRight))
                : new W.SectionProperties(
                    new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
                    PageSize(),
                    PageMarginOf(TOP_MARGIN, right: otherPagesRight));

            // The sections after the first one, each with the blocks before its end; the last one ends the body:
            var laterSections = new List<(OpenXmlElement[] Blocks, W.SectionProperties Properties)> { ([], mainSection) };
            if (options.SecondChapterSection)
                laterSections.Add((
                    [
                        new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = HEADING_1_STYLE_ID } }, new W.Run(new W.Text("Second chapter"))),
                        new W.Paragraph(new W.Run(new W.Text("More sample text."))),
                    ],
                    new W.SectionProperties(new W.SectionType { Val = W.SectionMarkValues.Continuous }, PageSize(), PageMarginOf(TOP_MARGIN, right: otherPagesRight, bottom: CHAPTER_BOTTOM_MARGIN))));

            if (options.AppendixPage)
            {
                var appendixHeaderPart = mainPart.AddNewPart<HeaderPart>();
                appendixHeaderPart.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text(APPENDIX_TEXT))));
                laterSections.Add((
                    [new W.Paragraph(new W.Run(new W.Text(APPENDIX_TEXT)))],
                    new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(appendixHeaderPart) }, PageSize(), PageMarginOf(TOP_MARGIN))));
            }

            foreach (var (blocks, properties) in laterSections)
            {
                body.Append(blocks);
                if (properties == laterSections[^1].Properties)
                    body.Append(properties);
                else
                    body.Append(new W.Paragraph(new W.ParagraphProperties { SectionProperties = properties }));
            }

            mainPart.Document = new W.Document(body);

            // The alternatives for older versions of Word name what they require by this prefix:
            mainPart.Document.AddNamespaceDeclaration("wps", WORDPROCESSING_SHAPE_NAMESPACE);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// The first section of a corporate template, which states the headers and footers for every
    /// section after it, and a header of its own for its first page.
    /// </summary>
    private static W.SectionProperties CoverSectionOf(MainDocumentPart mainPart, HeaderPart headerPart, HeaderPart coverHeaderPart, int left, bool footerShapes)
    {
        var footerPart = mainPart.AddNewPart<FooterPart>();
        var logoPart = footerPart.AddImagePart(ImagePartType.Png);
        using (var logo = new MemoryStream([137, 80, 78, 71]))
            logoPart.FeedData(logo);

        var footerParagraph = new W.Paragraph(new W.Run(PictureOf(footerPart.GetIdOfPart(logoPart))), new W.Run(new W.Text(FOOTER_TEXT)));
        if (footerShapes)
            footerParagraph.Append(FooterShapes());

        footerPart.Footer = new W.Footer(footerParagraph);
        return new W.SectionProperties(
            new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) },
            new W.HeaderReference { Type = W.HeaderFooterValues.First, Id = mainPart.GetIdOfPart(coverHeaderPart) },
            new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) },
            PageSize(),
            PageMarginOf(COVER_TOP_MARGIN, left: left),
            new W.TitlePage());
    }

    /// <summary>
    /// A paragraph style for the items of a list, which brings its numbering along.
    /// </summary>
    /// <param name="styleId">The ID of the style.</param>
    /// <param name="name">The name of the built-in style.</param>
    /// <param name="numberingInstance">The numbering instance the style brings along.</param>
    /// <param name="indentation">How far the text moves in, with the bullet or number hanging out
    /// by as much; null for the indentation of the numbering.</param>
    private static W.Style ListStyleOf(string styleId, string name, int numberingInstance, int? indentation = null) => new()
    {
        Type = W.StyleValues.Paragraph,
        StyleId = styleId,
        StyleName = new W.StyleName { Val = name },
        BasedOn = new W.BasedOn { Val = NORMAL_STYLE_ID },
        StyleParagraphProperties = new W.StyleParagraphProperties
        {
            NumberingProperties = new W.NumberingProperties { NumberingId = new W.NumberingId { Val = numberingInstance } },
            ContextualSpacing = new W.ContextualSpacing(),
            Indentation = indentation is { } left ? new W.Indentation { Left = left.ToString(), Hanging = left.ToString() } : null,
        },
    };

    /// <summary>
    /// A definition of a numbering, nine levels deep, which moves in by LIST_INDENTATION per level.
    /// </summary>
    private static W.AbstractNum ListDefinitionOf(int definitionId, W.NumberFormatValues format, Func<int, string> textOf)
    {
        var definition = new W.AbstractNum(new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel }) { AbstractNumberId = definitionId };
        for (var level = 0; level < 9; level++)
            definition.Append(new W.Level
            {
                LevelIndex = level,
                StartNumberingValue = new W.StartNumberingValue { Val = 1 },
                NumberingFormat = new W.NumberingFormat { Val = format },
                LevelText = new W.LevelText { Val = textOf(level) },
                LevelJustification = new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                PreviousParagraphProperties = new W.PreviousParagraphProperties
                {
                    Indentation = new W.Indentation { Left = (LIST_INDENTATION * (level + 1)).ToString(), Hanging = LIST_INDENTATION.ToString() },
                },
            });

        return definition;
    }

    /// <summary>
    /// A page of DIN A4.
    /// </summary>
    private static W.PageSize PageSize() => new() { Width = (uint)PAGE_WIDTH, Height = (uint)PAGE_HEIGHT };

    private static W.PageMargin PageMarginOf(int top, int left = SIDE_MARGIN, int right = SIDE_MARGIN, int bottom = BOTTOM_MARGIN) => new() { Top = top, Right = (uint)right, Bottom = bottom, Left = (uint)left, Header = 709U, Footer = 709U, Gutter = 0U };

    /// <summary>
    /// The shapes which float around the text of the first page: a picture with a caption in the
    /// left margin, a picture behind the whole page, and a sample picture within the text. The
    /// caption is a text box, which Word stores together with an alternative for older versions.
    /// </summary>
    /// <param name="mainPart">The main part of the template, which keeps the pictures.</param>
    /// <param name="left">The left margin of the first page, in twentieths of a point.</param>
    private static IEnumerable<W.Run> FirstPageShapesOf(MainDocumentPart mainPart, int left)
    {
        var columnStart = left * WordTextArea.EMU_PER_TWIP;
        var marginWidth = (left - 400) * WordTextArea.EMU_PER_TWIP;
        var square = () => new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides };

        yield return new W.Run(new W.Drawing(AnchorOf(
            10, MARGIN_PICTURE,
            HorizontalOf(DW.HorizontalRelativePositionValues.Column, MARGIN_PICTURE_LEFT - columnStart),
            VerticalOf(DW.VerticalRelativePositionValues.Paragraph, 0),
            marginWidth, marginWidth, false, square(), PictureGraphicOf(PictureIdOf(mainPart), marginWidth, marginWidth))));

        yield return new W.Run(new AlternateContent(
            new AlternateContentChoice(new W.Drawing(AnchorOf(
                11, MARGIN_CAPTION,
                HorizontalOf(DW.HorizontalRelativePositionValues.Column, MARGIN_PICTURE_LEFT - columnStart),
                VerticalOf(DW.VerticalRelativePositionValues.Page, 3_000_000),
                marginWidth, PICTURE_SIZE, false, square(), TextBoxGraphicOf(marginWidth, PICTURE_SIZE, new W.Paragraph(new W.Run(new W.Text(CAPTION_TEXT))))))) { Requires = "wps" },
            new AlternateContentFallback(new W.Picture())));

        var pageWidth = PAGE_WIDTH * WordTextArea.EMU_PER_TWIP;
        var pageHeight = PAGE_HEIGHT * WordTextArea.EMU_PER_TWIP;
        yield return new W.Run(new W.Drawing(AnchorOf(
            12, BACKGROUND_PICTURE,
            HorizontalOf(DW.HorizontalRelativePositionValues.Page, 0),
            VerticalOf(DW.VerticalRelativePositionValues.Page, 0),
            pageWidth, pageHeight, true, new DW.WrapNone(), PictureGraphicOf(PictureIdOf(mainPart), pageWidth, pageHeight))));

        yield return new W.Run(new W.Drawing(AnchorOf(
            13, SAMPLE_PICTURE,
            HorizontalOf(DW.HorizontalRelativePositionValues.Column, 0),
            VerticalOf(DW.VerticalRelativePositionValues.Paragraph, 0),
            PICTURE_SIZE, PICTURE_SIZE, false, square(), PictureGraphicOf(PictureIdOf(mainPart), PICTURE_SIZE, PICTURE_SIZE))));
    }

    /// <summary>
    /// The shape which floats around the text of the first page of the last section: a picture in the right margin.
    /// </summary>
    /// <param name="mainPart">The main part of the template, which keeps the picture.</param>
    /// <param name="right">The right margin of the last section, in twentieths of a point.</param>
    private static IEnumerable<W.Run> NextPageShapesOf(MainDocumentPart mainPart, int right)
    {
        var marginWidth = (right - 400) * WordTextArea.EMU_PER_TWIP;
        yield return new W.Run(new W.Drawing(AnchorOf(
            14, NEXT_PAGE_PICTURE,
            HorizontalOf(DW.HorizontalRelativePositionValues.Page, (PAGE_WIDTH - right + 200) * WordTextArea.EMU_PER_TWIP),
            VerticalOf(DW.VerticalRelativePositionValues.Paragraph, 0),
            marginWidth, marginWidth, false, new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides }, PictureGraphicOf(PictureIdOf(mainPart), marginWidth, marginWidth))));
    }

    /// <summary>
    /// The shapes which float in the footer: a text box with the address across the text, which
    /// reaches up FOOTER_CLEARANCE from the bottom edge of the page, and a logo higher up, but in
    /// the right margin beside the text.
    /// </summary>
    private static IEnumerable<W.Run> FooterShapes()
    {
        const long WIDTH = 2_000_000;
        var addressHeight = (FOOTER_CLEARANCE - 300) * WordTextArea.EMU_PER_TWIP;
        yield return new W.Run(new W.Drawing(AnchorOf(
            20, "Address",
            HorizontalOf(DW.HorizontalRelativePositionValues.Column, 0),
            VerticalOf(DW.VerticalRelativePositionValues.Page, (PAGE_HEIGHT - FOOTER_CLEARANCE) * WordTextArea.EMU_PER_TWIP),
            WIDTH, addressHeight, false, new DW.WrapNone(),
            TextBoxGraphicOf(WIDTH, addressHeight, new W.Paragraph(new W.Run(new W.Text("Street 1, 12345 Town")))))));

        var logoSize = 200 * WordTextArea.EMU_PER_TWIP;
        yield return new W.Run(new W.Drawing(AnchorOf(
            21, "Logo",
            HorizontalOf(DW.HorizontalRelativePositionValues.Page, (PAGE_WIDTH - 400) * WordTextArea.EMU_PER_TWIP),
            VerticalOf(DW.VerticalRelativePositionValues.Page, (PAGE_HEIGHT - 2 * FOOTER_CLEARANCE) * WordTextArea.EMU_PER_TWIP),
            logoSize, logoSize, true, new DW.WrapNone(),
            TextBoxGraphicOf(logoSize, logoSize, new W.Paragraph()))));
    }

    /// <summary>
    /// A table of contents, as a field with the result Word last computed, an entry for each of two
    /// chapters. Each entry has a field of its own for the page number.
    /// </summary>
    private static IEnumerable<W.Paragraph> TableOfContents()
    {
        yield return new W.Paragraph(
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
            new W.Run(new W.FieldCode(" TOC \\o \"1-3\" ") { Space = SpaceProcessingModeValues.Preserve }),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
            new W.Run(new W.Text(SAMPLE_CHAPTER)),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin }),
            new W.Run(new W.FieldCode(" PAGEREF _Toc1 \\h ") { Space = SpaceProcessingModeValues.Preserve }),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }),
            new W.Run(new W.Text("2")),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));

        yield return new W.Paragraph(
            new W.Run(new W.Text("Another chapter")),
            new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
    }

    /// <summary>
    /// A text box with a sample title above the text of the first page, below the top edge of the page.
    /// </summary>
    private static W.Run TitleBox()
    {
        const long WIDTH = 3_000_000;
        const long HEIGHT = 600_000;
        W.Paragraph TitleOf(string text) => new(
            new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = TITLE_STYLE_ID } },
            new W.Run(new W.RunProperties(new W.Color { Val = TITLE_COLOR }), new W.Text(text)));

        return new W.Run(new W.Drawing(AnchorOf(
            15, TITLE_BOX,
            HorizontalOf(DW.HorizontalRelativePositionValues.Column, 0),
            VerticalOf(DW.VerticalRelativePositionValues.Page, 100_000),
            WIDTH, HEIGHT, false, new DW.WrapNone(),
            TextBoxGraphicOf(
                WIDTH, HEIGHT,
                TitleOf(SAMPLE_TITLE),
                TitleOf(SAMPLE_TITLE_SECOND_LINE),
                new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = SUBTITLE_STYLE_ID } }, new W.Run(new W.Text(SAMPLE_SUBTITLE)))))));
    }

    /// <summary>
    /// A shape which floats around the text, the way Word anchors one at a paragraph.
    /// </summary>
    private static DW.Anchor AnchorOf(uint id, string name, DW.HorizontalPosition horizontal, DW.VerticalPosition vertical, long width, long height, bool behindText, OpenXmlElement wrap, A.Graphic graphic) => new(
        new DW.SimplePosition { X = 0L, Y = 0L },
        horizontal,
        vertical,
        new DW.Extent { Cx = width, Cy = height },
        new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
        wrap,
        new DW.DocProperties { Id = id, Name = name },
        new DW.NonVisualGraphicFrameDrawingProperties(),
        graphic)
    {
        DistanceFromTop = 0U,
        DistanceFromBottom = 0U,
        DistanceFromLeft = 0U,
        DistanceFromRight = 0U,
        SimplePos = false,
        RelativeHeight = id,
        BehindDoc = behindText,
        Locked = false,
        LayoutInCell = true,
        AllowOverlap = true,
    };

    private static DW.HorizontalPosition HorizontalOf(DW.HorizontalRelativePositionValues relativeFrom, long offset) => new(new DW.PositionOffset(offset.ToString(CultureInfo.InvariantCulture))) { RelativeFrom = relativeFrom };

    private static DW.VerticalPosition VerticalOf(DW.VerticalRelativePositionValues relativeFrom, long offset) => new(new DW.PositionOffset(offset.ToString(CultureInfo.InvariantCulture))) { RelativeFrom = relativeFrom };

    /// <summary>
    /// Adds a picture to the template.
    /// </summary>
    /// <returns>The ID of the relationship to the picture.</returns>
    private static string PictureIdOf(MainDocumentPart mainPart)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var picture = new MemoryStream([137, 80, 78, 71]))
            imagePart.FeedData(picture);

        return mainPart.GetIdOfPart(imagePart);
    }

    /// <summary>
    /// A picture in the line of text, the way Word inserts one.
    /// </summary>
    private static W.Drawing PictureOf(string relationshipId) => new(
        new DW.Inline(
            new DW.Extent { Cx = PICTURE_SIZE, Cy = PICTURE_SIZE },
            new DW.DocProperties { Id = 1U, Name = "Logo" },
            PictureGraphicOf(relationshipId, PICTURE_SIZE, PICTURE_SIZE))
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U,
        });

    private static A.Graphic PictureGraphicOf(string relationshipId, long width, long height) => new(
        new A.GraphicData(
            new PIC.Picture(
                new PIC.NonVisualPictureProperties(
                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" },
                    new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(
                    new A.Blip { Embed = relationshipId },
                    new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = width, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
        {
            Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
        });

    /// <summary>
    /// A text box, the way Word 2010 and later store one.
    /// </summary>
    private static A.Graphic TextBoxGraphicOf(long width, long height, params W.Paragraph[] paragraphs) => new(
        new A.GraphicData(
            new WPS.WordprocessingShape(
                new WPS.NonVisualDrawingShapeProperties { TextBox = true },
                new WPS.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = width, Cy = height }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }),
                new WPS.TextBoxInfo2(new W.TextBoxContent(paragraphs)),
                new WPS.TextBodyProperties()))
        {
            Uri = WORDPROCESSING_SHAPE_NAMESPACE,
        });
}