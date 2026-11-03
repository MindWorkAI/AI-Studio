using DocumentFormat.OpenXml;

using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Estimates how much room paragraphs and tables take up on a page.
/// </summary>
/// <remarks>
/// Only Word knows where it breaks a page, once it has laid the text out with the fonts at hand.
/// The file states nothing of it. The estimate counts the lines of each paragraph from the number
/// of its characters, with the width of a character in a common font, and from the font size, line
/// spacing, and spacing its styles state; a picture in a line adds its height. It serves to find
/// where the paragraphs of a template start, so it aims at the room they take up. A page filled by
/// the estimate keeps some room to spare, see FILL, for fonts with wider characters.
/// </remarks>
/// <param name="styles">The styles of the document; null when it has none.</param>
/// <param name="numbering">The numbering of the lists, which states how far their items move in.</param>
internal sealed class WordLayoutEstimate(W.Styles? styles, WordNumbering numbering)
{
    /// <summary>
    /// The share of a page which the estimate fills, so that the page does not overflow when the text takes more room than estimated.
    /// </summary>
    public const double FILL = 0.9;

    /// <summary>
    /// The width of a character, as a share of the font size. It includes the room which is lost where
    /// a line breaks between two words. Measured in Word, text in a sans-serif font such as Calibri
    /// needs about 0.4.
    /// </summary>
    public const double CHARACTER_WIDTH = 0.45;

    /// <summary>
    /// The height of a line with single spacing, as a share of the font size.
    /// </summary>
    public const double LINE_HEIGHT = 1.2;

    /// <summary>
    /// The font size of text whose styles state none, in half points: Word uses 10 pt then.
    /// </summary>
    private const int DEFAULT_FONT_SIZE = 20;

    /// <summary>
    /// The space a table cell keeps between its edges and its text, on either side.
    /// </summary>
    private const int CELL_MARGIN = 108;

    private readonly Dictionary<string, W.Style> paragraphStyles = styles?.Elements<W.Style>()
        .Where(style => (style.Type?.InnerText ?? "paragraph") == "paragraph" && style.StyleId?.Value is not null)
        .GroupBy(style => style.StyleId!.Value!)
        .ToDictionary(group => group.Key, group => group.First()) ?? new Dictionary<string, W.Style>();

    private readonly W.DocDefaults? docDefaults = styles?.DocDefaults;

    /// <summary>
    /// Counts how many blocks fit on the first page of a section, with room to spare.
    /// </summary>
    /// <remarks>
    /// A block which keeps with the next one, such as a heading, never ends the page, and neither do
    /// empty paragraphs before it. The first block of the text is on the page in any case, even when
    /// the estimate finds it too large, so that the page never stays empty.
    /// </remarks>
    /// <param name="blocks">The paragraphs and tables, in the order of the document.</param>
    /// <param name="area">The page of the section.</param>
    /// <returns>The number of blocks from the start which go on the page; all of them when they fit.</returns>
    public int CountFittingOnFirstPage(IReadOnlyList<OpenXmlElement> blocks, WordTextArea area)
    {
        var room = (long)(area.ColumnHeight * (double)area.Columns * FILL);
        var count = 0;
        for (var used = 0L; count < blocks.Count; count++)
        {
            used += this.HeightOf(blocks[count], area.ColumnWidth);
            if (used > room)
                break;
        }

        if (count == blocks.Count)
            return count;

        while (count > 0 && (IsEmptyParagraph(blocks[count - 1]) || this.KeepsWithNext(blocks[count - 1])))
            count--;

        var firstBlock = 0;
        while (firstBlock < blocks.Count - 1 && (IsEmptyParagraph(blocks[firstBlock]) || this.KeepsWithNext(blocks[firstBlock])))
            firstBlock++;

        return Math.Max(count, firstBlock + 1);
    }

    /// <summary>
    /// Estimates the height of a paragraph or a table.
    /// </summary>
    /// <param name="block">The paragraph or table.</param>
    /// <param name="width">The width of the column it stands in.</param>
    /// <returns>The height, in twentieths of a point; 0 for anything else.</returns>
    public int HeightOf(OpenXmlElement block, int width) => block switch
    {
        W.Paragraph paragraph => this.HeightOf(paragraph, width),
        W.Table table => table.Elements<W.TableRow>().Sum(row => this.HeightOf(row, width)),

        _ => 0,
    };

    /// <summary>
    /// Determines whether a block is a paragraph without any text, which takes up the height of an empty line.
    /// </summary>
    public static bool IsEmptyParagraph(OpenXmlElement block) => block is W.Paragraph paragraph && !paragraph.OwnDescendants<W.Text>().Any();

    /// <summary>
    /// Determines whether a block keeps with the next one on its page, as a heading does.
    /// </summary>
    public bool KeepsWithNext(OpenXmlElement block) => block is W.Paragraph paragraph && this.LookOf(paragraph).KeepsWithNext;

    private int HeightOf(W.Paragraph paragraph, int width)
    {
        var look = this.LookOf(paragraph);
        var textWidth = Math.Max(width - look.Left - look.Right, width / 4);
        var characterWidth = look.FontSize * 10 * CHARACTER_WIDTH;
        var lines = LineLengthsOf(paragraph).Sum(length => Math.Max(1, (int)Math.Ceiling(length * characterWidth / textWidth)));

        var singleLine = look.FontSize * 10 * LINE_HEIGHT;
        var lineHeight = look.LineRule switch
        {
            "exact" => look.Line,
            "atLeast" => Math.Max(look.Line, singleLine),

            _ => singleLine * look.Line / 240,
        };

        var pictures = paragraph.OwnDescendants<DW.Inline>().Sum(picture => (picture.Extent?.Cy?.Value ?? 0) / WordTextArea.EMU_PER_TWIP);
        return (int)(look.SpaceBefore + lines * lineHeight + pictures + look.SpaceAfter);
    }

    private int HeightOf(W.TableRow row, int width)
    {
        var cells = row.Elements<W.TableCell>().ToList();
        if (cells.Count is 0)
            return 0;

        var cellWidth = Math.Max(width / cells.Count - 2 * CELL_MARGIN, 1);
        return cells.Max(cell => cell.ChildElements.Sum(block => this.HeightOf(block, cellWidth)));
    }

    /// <summary>
    /// Returns the number of characters of each line which the paragraph breaks itself, e.g., code.
    /// The text of text boxes, which float elsewhere, does not count.
    /// </summary>
    private static List<int> LineLengthsOf(W.Paragraph paragraph)
    {
        var lengths = new List<int> { 0 };
        foreach (var element in paragraph.OwnDescendants<OpenXmlElement>())
            switch (element)
            {
                case W.Text text:
                    lengths[^1] += text.Text.Length;
                    break;

                case W.TabChar:
                    lengths[^1] += 4;
                    break;

                case W.Break or W.CarriageReturn:
                    lengths.Add(0);
                    break;
            }

        return lengths;
    }

    /// <summary>
    /// Reads what decides the height of a paragraph: the defaults of the document, then its styles
    /// from the one all others are based on, then its own properties. A list item without an
    /// indentation of its own moves in as far as its numbering says.
    /// </summary>
    private Look LookOf(W.Paragraph paragraph)
    {
        var look = new Look();
        look.ApplyParagraph(this.docDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle);
        look.ApplyRun(this.docDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle);

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? this.paragraphStyles.Values.FirstOrDefault(style => style.Default?.Value is true)?.StyleId?.Value;
        foreach (var style in this.StyleChainOf(styleId))
        {
            look.ApplyParagraph(style.StyleParagraphProperties);
            look.ApplyRun(style.StyleRunProperties);
        }

        var properties = paragraph.ParagraphProperties;
        look.ApplyParagraph(properties);
        look.ApplyRun(properties?.ParagraphMarkRunProperties);
        foreach (var runProperties in paragraph.Descendants<W.RunProperties>())
            if (int.TryParse(runProperties.FontSize?.Val?.Value, out var size))
                look.FontSize = Math.Max(look.FontSize, size);

        if (properties?.Indentation is null && properties?.NumberingProperties is { NumberingId.Val.Value: > 0 and var instance } numberingProperties)
            look.Left = numbering.TextIndentationOf(instance, numberingProperties.NumberingLevelReference?.Val?.Value ?? 0);

        return look;
    }

    /// <summary>
    /// Returns a paragraph style and the styles it is based on, starting with the one all others are based on.
    /// </summary>
    private List<W.Style> StyleChainOf(string? styleId)
    {
        var chain = new List<W.Style>();
        while (styleId is not null && this.paragraphStyles.TryGetValue(styleId, out var style) && !chain.Contains(style))
        {
            chain.Insert(0, style);
            styleId = style.BasedOn?.Val?.Value;
        }

        return chain;
    }

    /// <summary>
    /// What decides the height of a paragraph, in twentieths of a point unless stated otherwise.
    /// </summary>
    private sealed class Look
    {
        private int before;
        private int after;
        private int? beforeLines;
        private int? afterLines;
        private bool beforeAutomatically;
        private bool afterAutomatically;

        /// <summary>
        /// The font size, in half points.
        /// </summary>
        public int FontSize { get; set; } = DEFAULT_FONT_SIZE;

        /// <summary>
        /// The line spacing: a height for an exact or a least one, in 240ths of a single line for an automatic one.
        /// </summary>
        public int Line { get; private set; } = 240;

        public string LineRule { get; private set; } = "auto";

        public int Left { get; set; }

        public int Right { get; private set; }

        public bool KeepsWithNext { get; private set; }

        /// <summary>
        /// The space above the paragraph. Spacing in lines counts lines of 12 pt, and automatic spacing
        /// is what Word leaves around paragraphs of web pages.
        /// </summary>
        public int SpaceBefore => this.beforeAutomatically ? 280 : this.beforeLines is { } lines ? lines * 240 / 100 : this.before;

        /// <summary>
        /// The space below the paragraph, counted as SpaceBefore is.
        /// </summary>
        public int SpaceAfter => this.afterAutomatically ? 280 : this.afterLines is { } lines ? lines * 240 / 100 : this.after;

        /// <summary>
        /// Takes over what paragraph properties state, e.g., those of a style.
        /// </summary>
        public void ApplyParagraph(OpenXmlElement? properties)
        {
            if (properties is null)
                return;

            if (properties.GetFirstChild<W.SpacingBetweenLines>() is { } spacing)
            {
                if (int.TryParse(spacing.Before?.Value, out var spaceBefore))
                    this.before = spaceBefore;

                if (int.TryParse(spacing.After?.Value, out var spaceAfter))
                    this.after = spaceAfter;

                if (spacing.BeforeLines?.Value is { } linesBefore)
                    this.beforeLines = linesBefore;

                if (spacing.AfterLines?.Value is { } linesAfter)
                    this.afterLines = linesAfter;

                if (spacing.BeforeAutoSpacing?.Value is { } automaticallyBefore)
                    this.beforeAutomatically = automaticallyBefore;

                if (spacing.AfterAutoSpacing?.Value is { } automaticallyAfter)
                    this.afterAutomatically = automaticallyAfter;

                if (int.TryParse(spacing.Line?.Value, out var line))
                    this.Line = line;

                if (spacing.LineRule is { } rule)
                    this.LineRule = rule.InnerText;
            }

            if (properties.GetFirstChild<W.Indentation>() is { } indentation)
            {
                if (WordNumbering.LeftOf(indentation) is { } left)
                    this.Left = left;

                if (int.TryParse(indentation.Right?.Value ?? indentation.End?.Value, out var right))
                    this.Right = right;
            }

            if (properties.GetFirstChild<W.KeepNext>() is { } keepNext)
                this.KeepsWithNext = keepNext.Val?.Value is not false;
        }

        /// <summary>
        /// Takes over what run properties state, e.g., those of a style.
        /// </summary>
        public void ApplyRun(OpenXmlElement? properties)
        {
            if (int.TryParse(properties?.GetFirstChild<W.FontSize>()?.Val?.Value, out var size))
                this.FontSize = size;
        }
    }
}