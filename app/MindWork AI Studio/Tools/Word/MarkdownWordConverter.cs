using System.Diagnostics.CodeAnalysis;

using AIStudio.Tools.OpenXml;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Converts Markdown into the paragraphs of a Word document.
/// </summary>
/// <remarks>
/// Markdig reads the Markdown with the pipeline AI Studio uses everywhere else, so the document
/// holds what the chat shows. A line break within a paragraph becomes a space, as with Pandoc,
/// which writes a document without a template. Every paragraph and every text takes its look from
/// the styles of the template; only what Markdown itself marks, such as bold text, is formatted
/// directly.
/// </remarks>
/// <param name="mainPart">The main part of the document, which keeps the targets of the links.</param>
/// <param name="styles">The styles of the template.</param>
internal sealed class MarkdownWordConverter(MainDocumentPart mainPart, WordStyles styles)
{
    /// <summary>
    /// The kinds of link targets a reader of the document can follow. A link to a heading of the
    /// answer, for instance, leads nowhere in the document.
    /// </summary>
    private static readonly HashSet<string> LINK_SCHEMES = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "file", "ftp" };

    /// <summary>
    /// Converts the given Markdown.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <returns>The paragraphs, in the order the document lists them.</returns>
    public List<OpenXmlElement> Convert(string markdown)
    {
        var elements = new List<OpenXmlElement>();
        foreach (var block in Markdig.Markdown.Parse(markdown, Markdown.SAFE_MARKDOWN_PIPELINE))
            this.AddBlock(block, elements);

        return elements;
    }

    private void AddBlock(Markdig.Syntax.Block block, List<OpenXmlElement> elements)
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

            case LeafBlock { Inline: { } inline }:
                elements.Add(this.ParagraphOf(inline, null));
                break;

            // A block whose text Markdig keeps as it is stays as written, one paragraph per line:
            case LeafBlock leaf:
                foreach (var line in LinesOf(leaf))
                    elements.Add(new W.Paragraph(RunOf(line, RunFormat.PLAIN)));

                break;

            case ContainerBlock container:
                foreach (var child in container)
                    this.AddBlock(child, elements);

                break;
        }
    }

    private W.Paragraph ParagraphOf(ContainerInline? inlines, string? styleId)
    {
        var paragraph = new W.Paragraph();
        if (styleId is not null)
            paragraph.Append(new W.ParagraphProperties { ParagraphStyleId = new W.ParagraphStyleId { Val = styleId } });

        if (inlines is not null)
            this.AppendInlines(paragraph, inlines, RunFormat.PLAIN);

        return paragraph;
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

                // Word has no notation of its own for TeX, so math stays as the model wrote it:
                case MathInline math:
                    var delimiter = new string(math.Delimiter, math.DelimiterCount);
                    AppendText(target, $"{delimiter}{math.Content}{delimiter}", format);
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