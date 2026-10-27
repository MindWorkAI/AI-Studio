using System.Diagnostics.CodeAnalysis;

using AIStudio.Tools.OpenXml;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Converts Markdown into the paragraphs and tables of a Word document.
/// </summary>
/// <remarks>
/// Markdig reads the Markdown with the pipeline AI Studio uses everywhere else, so the document
/// holds what the chat shows. A line break within a paragraph becomes a space, as with Pandoc,
/// which writes a document without a template. Every paragraph and every text takes its look from
/// the styles of the template; only what Markdown itself marks, such as bold text or the alignment
/// of a column, is formatted directly.
///
/// Some things Markdown knows have no counterpart which a template could style: math stays in the
/// notation of TeX, a picture is replaced by its description, and the footnotes follow the text as
/// a list, with their numbers raised, rather than going to the foot of the page.
/// </remarks>
/// <param name="mainPart">The main part of the document, which keeps the targets of the links.</param>
/// <param name="styles">The styles of the template.</param>
/// <param name="numbering">The numbering of the lists.</param>
internal sealed class MarkdownWordConverter(MainDocumentPart mainPart, WordStyles styles, WordNumbering numbering)
{
    /// <summary>
    /// The kinds of link targets a reader of the document can follow. A link to a heading of the
    /// answer, for instance, leads nowhere in the document.
    /// </summary>
    private static readonly HashSet<string> LINK_SCHEMES = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "file", "ftp" };

    /// <summary>
    /// The width of a table, in fiftieths of a percent of the width of the text: all of it.
    /// </summary>
    private const int FULL_TABLE_WIDTH = 5000;

    /// <summary>
    /// The width the columns of a table share, in twentieths of a point. Only the proportions of the
    /// columns count: the table spans the width of the text, which Word and LibreOffice divide accordingly.
    /// </summary>
    private const int GRID_WIDTH = 9000;

    /// <summary>
    /// Converts the given Markdown.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>The paragraphs and tables, in the order the document lists them. The last one is a paragraph.</returns>
    public List<OpenXmlElement> Convert(string markdown)
    {
        var elements = new List<OpenXmlElement>();
        foreach (var block in Markdig.Markdown.Parse(markdown, Markdown.SAFE_MARKDOWN_PIPELINE))
            this.AddBlock(block, elements);

        EndWithParagraph(elements);
        return elements;
    }

    /// <summary>
    /// Adds a block of the Markdown to the document.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="elements">The paragraphs and tables of the document so far.</param>
    /// <param name="paragraphStyleId">The style of a plain paragraph within the block, e.g., of a quotation; null for the default style.</param>
    private void AddBlock(Markdig.Syntax.Block block, List<OpenXmlElement> elements, string? paragraphStyleId = null)
    {
        switch (block)
        {
            case HeadingBlock heading:
                elements.Add(this.ParagraphOf(heading.Inline, styles.HeadingOf(heading.Level)));
                break;

            case ThematicBreakBlock:
                elements.Add(ThematicBreak());
                break;

            // The targets of reference links, which the links carry already:
            case LinkReferenceDefinitionGroup or LinkReferenceDefinition:
                break;

            // Word has no notation of its own for TeX, so math stays as the model wrote it:
            case MathBlock math:
                var fence = new string(math.FencedChar, math.OpeningFencedCharCount);
                elements.Add(ParagraphOfLines([fence, ..LinesOf(math), fence], paragraphStyleId));
                break;

            case CodeBlock code:
                elements.Add(ParagraphOfLines(LinesOf(code), styles.CodeBlock));
                break;

            case LeafBlock { Inline: { } inline }:
                elements.Add(this.ParagraphOf(inline, paragraphStyleId));
                break;

            // A block whose text Markdig keeps as it is stays as written:
            case LeafBlock leaf:
                elements.Add(ParagraphOfLines(LinesOf(leaf), paragraphStyleId));
                break;

            case ListBlock list:
                this.AddList(list, elements, 0);
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    this.AddBlock(child, elements, styles.Quote);

                break;

            case Table table:
                // Word joins two tables in a row into one:
                if (elements.LastOrDefault() is W.Table)
                    elements.Add(new W.Paragraph());

                elements.Add(this.TableOf(table));
                break;

            case FootnoteGroup footnotes:
                this.AddFootnotes(footnotes, elements);
                break;

            case ContainerBlock container:
                foreach (var child in container)
                    this.AddBlock(child, elements, paragraphStyleId);

                break;
        }
    }

    private W.Paragraph ParagraphOf(ContainerInline? inlines, string? styleId)
    {
        var paragraph = ParagraphWithStyle(styleId);
        if (inlines is not null)
            this.AppendInlines(paragraph, inlines, RunFormat.PLAIN);

        return paragraph;
    }

    private static W.Paragraph ParagraphWithStyle(string? styleId) => styleId is null
        ? new W.Paragraph()
        : new W.Paragraph(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = styleId } });

    /// <summary>
    /// One paragraph for lines which have to stay as they are, such as code: each line starts on
    /// a new line of the paragraph, and spaces and tabs stay as well.
    /// </summary>
    private static W.Paragraph ParagraphOfLines(IEnumerable<string> lines, string? styleId)
    {
        var paragraph = ParagraphWithStyle(styleId);
        var isFirstLine = true;
        foreach (var line in lines)
        {
            if (!isFirstLine)
                paragraph.Append(new W.Run(new W.Break()));

            var run = new W.Run();
            var pieces = OpenXmlText.XmlSafe(line).Split('\t');
            for (var index = 0; index < pieces.Length; index++)
            {
                if (index > 0)
                    run.Append(new W.TabChar());

                if (pieces[index].Length > 0)
                    run.Append(new W.Text(pieces[index]) { Space = SpaceProcessingModeValues.Preserve });
            }

            paragraph.Append(run);
            isFirstLine = false;
        }

        return paragraph;
    }

    /// <summary>
    /// Adds a list, and the lists its items hold one level further in.
    /// </summary>
    /// <remarks>
    /// The first paragraph of an item carries the bullet or number. Further paragraphs of the item
    /// start where the text of the first one does, and other blocks, such as code, stand as they would
    /// outside the list.
    /// </remarks>
    /// <param name="list">The list.</param>
    /// <param name="elements">The paragraphs and tables of the document so far.</param>
    /// <param name="level">The level of the list, 0 for a list which no other list holds.</param>
    private void AddList(ListBlock list, List<OpenXmlElement> elements, int level)
    {
        level = Math.Min(level, WordNumbering.LEVELS - 1);
        var instance = numbering.InstanceFor(list.IsOrdered, int.TryParse(list.OrderedStart, out var start) ? start : 1, level);
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var isFirstBlock = true;
            foreach (var child in item)
            {
                switch (child)
                {
                    case ListBlock innerList:
                        this.AddList(innerList, elements, level + 1);
                        break;

                    case ParagraphBlock paragraphBlock:
                        var paragraph = this.ParagraphOf(paragraphBlock.Inline, styles.ListParagraph);
                        var properties = paragraph.ParagraphProperties!;
                        if (isFirstBlock)
                            properties.NumberingProperties = new W.NumberingProperties
                            {
                                NumberingLevelReference = new W.NumberingLevelReference { Val = level },
                                NumberingId = new W.NumberingId { Val = instance },
                            };
                        else
                            properties.Indentation = new W.Indentation { Left = WordNumbering.IndentationOf(level).ToString() };

                        elements.Add(paragraph);
                        break;

                    default:
                        this.AddBlock(child, elements);
                        break;
                }

                isFirstBlock = false;
            }
        }
    }

    /// <summary>
    /// Converts a table, which takes the table style of the template and the width of the text.
    /// </summary>
    /// <remarks>
    /// A header row repeats on every page the table covers. Whether it looks different from the
    /// other rows is up to the table style.
    /// </remarks>
    private W.Table TableOf(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        var columns = Math.Max(1, rows.Select(row => row.Count).DefaultIfEmpty(0).Max());
        var result = new W.Table(
            new W.TableProperties
            {
                TableStyle = new W.TableStyle { Val = styles.Table },
                TableWidth = new W.TableWidth { Width = FULL_TABLE_WIDTH.ToString(), Type = W.TableWidthUnitValues.Pct },
                TableLook = new W.TableLook { Val = "0420", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = false, NoVerticalBand = true },
            },
            new W.TableGrid(Enumerable.Range(0, columns).Select(_ => new W.GridColumn { Width = (GRID_WIDTH / columns).ToString() })));

        foreach (var row in rows)
        {
            var tableRow = new W.TableRow();
            if (row.IsHeader)
                tableRow.Append(new W.TableRowProperties(new W.TableHeader()));

            var cells = row.OfType<TableCell>().ToList();
            for (var column = 0; column < columns; column++)
                tableRow.Append(this.CellOf(column < cells.Count ? cells[column] : null, AlignmentOf(table, column), columns));

            result.Append(tableRow);
        }

        return result;
    }

    /// <summary>
    /// Converts a cell of a table; a row with fewer cells than the table has columns gets empty ones.
    /// </summary>
    private W.TableCell CellOf(TableCell? cell, W.JustificationValues? alignment, int columns)
    {
        var content = new List<OpenXmlElement>();
        if (cell is not null)
            foreach (var block in cell)
                this.AddBlock(block, content);

        // Word wants a cell to end with a paragraph, as it does the whole document:
        EndWithParagraph(content);
        if (alignment is { } justification)
            foreach (var paragraph in content.OfType<W.Paragraph>())
                (paragraph.ParagraphProperties ??= new W.ParagraphProperties()).Justification = new W.Justification { Val = justification };

        var tableCell = new W.TableCell(new W.TableCellProperties
        {
            TableCellWidth = new W.TableCellWidth { Width = (FULL_TABLE_WIDTH / columns).ToString(), Type = W.TableWidthUnitValues.Pct },
        });

        tableCell.Append(content);
        return tableCell;
    }

    /// <summary>
    /// The alignment of a column, as the line below the header of the table states it; null for the
    /// left, where the template aligns text anyway.
    /// </summary>
    private static W.JustificationValues? AlignmentOf(Table table, int column)
    {
        if (column >= table.ColumnDefinitions.Count)
            return null;

        return table.ColumnDefinitions[column].Alignment switch
        {
            TableColumnAlign.Center => W.JustificationValues.Center,
            TableColumnAlign.Right => W.JustificationValues.Right,

            _ => null,
        };
    }

    /// <summary>
    /// Adds the footnotes below a line, each starting with its raised number, the way the chat shows them.
    /// </summary>
    private void AddFootnotes(FootnoteGroup footnotes, List<OpenXmlElement> elements)
    {
        elements.Add(ThematicBreak());
        foreach (var footnote in footnotes.OfType<Footnote>())
        {
            var note = new List<OpenXmlElement>();
            foreach (var block in footnote)
                this.AddBlock(block, note);

            if (note.FirstOrDefault() is not W.Paragraph firstParagraph)
                note.Insert(0, firstParagraph = new W.Paragraph());

            var number = RunOf(footnote.Order.ToString(), RunFormat.PLAIN with { Position = W.VerticalPositionValues.Superscript });
            var space = RunOf(" ", RunFormat.PLAIN);
            if (firstParagraph.ParagraphProperties is { } properties)
                properties.InsertAfterSelf(number).InsertAfterSelf(space);
            else
                firstParagraph.PrependChild(space).InsertBeforeSelf(number);

            elements.AddRange(note);
        }
    }

    /// <summary>
    /// Adds an empty paragraph when the content ends with something else, i.e., with a table, or
    /// when there is no content at all. Word wants a body and a table cell to end with a paragraph,
    /// and offers to repair a file which does not.
    /// </summary>
    private static void EndWithParagraph(List<OpenXmlElement> elements)
    {
        if (elements.LastOrDefault() is not W.Paragraph)
            elements.Add(new W.Paragraph());
    }

    /// <summary>
    /// A line across the page, the way Word draws one: as the lower border of an empty paragraph.
    /// </summary>
    private static W.Paragraph ThematicBreak() => new(new W.ParagraphProperties
    {
        ParagraphBorders = new W.ParagraphBorders
        {
            BottomBorder = new W.BottomBorder { Val = W.BorderValues.Single, Size = 6U, Space = 1U, Color = "auto" },
        },
    });

    private void AppendInlines(OpenXmlCompositeElement target, ContainerInline inlines, RunFormat format)
    {
        foreach (var inline in inlines)
            switch (inline)
            {
                case LiteralInline literal:
                    AppendText(target, literal.Content.ToString(), format);
                    break;

                case HtmlEntityInline entity:
                    AppendText(target, entity.Transcoded.ToString(), format);
                    break;

                case CodeInline code:
                    AppendText(target, code.Content, format with { CharacterStyle = styles.InlineCode });
                    break;

                case MathInline math:
                    var delimiter = new string(math.Delimiter, math.DelimiterCount);
                    AppendText(target, $"{delimiter}{math.Content}{delimiter}", format);
                    break;

                // The box of an item of a task list; the text after it starts with a space:
                case TaskList task:
                    AppendText(target, task.Checked ? "☑" : "☐", format);
                    break;

                // Each footnote names its way back to the text, which a printed page cannot follow:
                case FootnoteLink { IsBackLink: true }:
                    break;

                case FootnoteLink footnoteLink:
                    AppendText(target, footnoteLink.Footnote.Order.ToString(), format with { Position = W.VerticalPositionValues.Superscript });
                    break;

                case LineBreakInline { IsHard: true }:
                    target.Append(new W.Run(new W.Break()));
                    break;

                case LineBreakInline:
                    AppendText(target, " ", format);
                    break;

                case AutolinkInline autolink:
                    this.AppendLink(target, autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url, null, autolink.Url, format);
                    break;

                // A picture of the answer is a link to a file which the document cannot carry, so its description stands in for it:
                case LinkInline { IsImage: true } image:
                    this.AppendInlines(target, image, format);
                    break;

                case LinkInline link:
                    this.AppendLink(target, link.Url, link, link.Url ?? string.Empty, format);
                    break;

                case EmphasisInline emphasis:
                    this.AppendInlines(target, emphasis, FormatOf(emphasis, format));
                    break;

                case ContainerInline container:
                    this.AppendInlines(target, container, format);
                    break;
            }
    }

    /// <summary>
    /// Appends a link, or only its text when the document cannot lead anywhere with it.
    /// </summary>
    /// <param name="target">The paragraph to append the link to.</param>
    /// <param name="url">Where the link leads.</param>
    /// <param name="text">The text of the link; null when the link shows its target.</param>
    /// <param name="fallbackText">What the link shows when it has no text.</param>
    /// <param name="format">The formatting around the link.</param>
    private void AppendLink(OpenXmlCompositeElement target, string? url, ContainerInline? text, string fallbackText, RunFormat format)
    {
        if (!TryReadLinkTarget(url, out var uri))
        {
            if (text is not null)
                this.AppendInlines(target, text, format);
            else
                AppendText(target, fallbackText, format);

            return;
        }

        var relationship = mainPart.AddHyperlinkRelationship(uri, true);
        var hyperlink = new W.Hyperlink { Id = relationship.Id, History = true };
        var linkFormat = format with { CharacterStyle = styles.Hyperlink };
        if (text is not null)
            this.AppendInlines(hyperlink, text, linkFormat);

        if (!hyperlink.HasChildren)
            AppendText(hyperlink, fallbackText, linkFormat);

        target.Append(hyperlink);
    }

    /// <remarks>
    /// The scheme has to be written out: on Linux and macOS, .NET takes a path such as /docs for
    /// a file, which a link in an answer hardly ever means.
    /// </remarks>
    private static bool TryReadLinkTarget(string? url, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        return !string.IsNullOrWhiteSpace(url)
               && Uri.TryCreate(url, UriKind.Absolute, out uri)
               && LINK_SCHEMES.Contains(uri.Scheme)
               && url.StartsWith($"{uri.Scheme}:", StringComparison.OrdinalIgnoreCase);
    }

    private static RunFormat FormatOf(EmphasisInline emphasis, RunFormat format) => (emphasis.DelimiterChar, emphasis.DelimiterCount) switch
    {
        ('*' or '_', 1) => format with { Italic = true },
        ('*' or '_', _) => format with { Bold = true },
        ('~', 1) => format with { Position = W.VerticalPositionValues.Subscript },
        ('~', _) => format with { Strike = true },
        ('^', _) => format with { Position = W.VerticalPositionValues.Superscript },
        ('+', _) => format with { Underline = true },
        ('=', _) => format with { Highlight = true },

        _ => format,
    };

    private static void AppendText(OpenXmlCompositeElement target, string text, RunFormat format)
    {
        if (text.Length > 0)
            target.Append(RunOf(text, format));
    }

    private static W.Run RunOf(string text, RunFormat format)
    {
        var run = new W.Run();
        if (format != RunFormat.PLAIN)
            run.Append(new W.RunProperties
            {
                RunStyle = format.CharacterStyle is { } style ? new W.RunStyle { Val = style } : null,
                Bold = format.Bold ? new W.Bold() : null,
                Italic = format.Italic ? new W.Italic() : null,
                Strike = format.Strike ? new W.Strike() : null,
                Highlight = format.Highlight ? new W.Highlight { Val = W.HighlightColorValues.Yellow } : null,
                Underline = format.Underline ? new W.Underline { Val = W.UnderlineValues.Single } : null,
                VerticalTextAlignment = format.Position is { } position ? new W.VerticalTextAlignment { Val = position } : null,
            });

        run.Append(new W.Text(OpenXmlText.XmlSafe(text)) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static IEnumerable<string> LinesOf(LeafBlock leaf)
    {
        for (var index = 0; index < leaf.Lines.Count; index++)
            yield return leaf.Lines.Lines[index].Slice.ToString();
    }

    /// <summary>
    /// How Markdown marks a piece of text, which the runs of that text carry.
    /// </summary>
    /// <param name="CharacterStyle">The ID of a character style, e.g., of a link; null for none.</param>
    private sealed record RunFormat(
        bool Bold = false,
        bool Italic = false,
        bool Strike = false,
        bool Underline = false,
        bool Highlight = false,
        W.VerticalPositionValues? Position = null,
        string? CharacterStyle = null)
    {
        public static readonly RunFormat PLAIN = new();
    }
}