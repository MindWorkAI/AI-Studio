using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Markdown files, whose paragraphs, headings, list items, quotes, table cells, and footnotes a model works
/// on, while code, formulas, link targets, front matter, and embedded HTML stay as they are.
/// </summary>
/// <remarks>
/// <para>
/// The segments come from the positions Markdig reads for each part of the text, so the new document is the
/// source with the words of each segment replaced, and everything else of the file stays byte for byte.
/// </para>
/// <para>
/// Emphasis and links become pairs of tags, which stand for their markup, e.g., "**" or "](url)", so a
/// model moves them with their words and never touches an address. Code spans, formulas, autolinks, inline
/// HTML, footnote references, task boxes, and hard line breaks become tags of their own. A soft line break
/// becomes a space: the new words of a paragraph go into one line, which Markdown shows the same way.
/// </para>
/// <para>
/// Words travel decoded and come back encoded, as for web pages and LaTeX: the model gets them as they read, e.g.,
/// "a | b" for "a \| b" or "&amp;" for "&amp;amp;", and MarkdownWords escapes the new words where Markdown would
/// take them for markup. The white space around the words of a segment, e.g., the padding of a table cell, stays
/// outside of it.
/// </para>
/// </remarks>
public sealed class MarkdownSegmentFormat : TextSegmentFormat
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MarkdownSegmentFormat).Namespace, nameof(MarkdownSegmentFormat));

    /// <summary>
    /// Reads where each part of the text lies. HTML is read as HTML, so that it becomes tags rather than
    /// words; nothing here renders it.
    /// </summary>
    private static readonly MarkdownPipeline PIPELINE = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .UsePreciseSourceLocation()
        .Build();

    /// <inheritdoc />
    public override FileTypeFilter FileType => FileTypes.MARKDOWN;

    /// <inheritdoc />
    public override string LimitsNotice => TB("Code, formulas, link targets, and embedded HTML stay as they are.");

    /// <inheritdoc />
    protected override IReadOnlyList<TextSegment> ReadSegments(string text) => SegmentsOf(text);

    /// <inheritdoc />
    protected override string EncodedWords(string words) => MarkdownWords.Encoded(words);

    /// <summary>
    /// Reads the segments of a Markdown text, in the order of Markdig, which puts the footnotes last.
    /// </summary>
    /// <remarks>
    /// A block whose words read as a tag stays out: a model could not tell them apart from the tags which
    /// guard its addresses and code, so the block stays as it is.
    /// </remarks>
    /// <param name="text">The Markdown text.</param>
    public static IReadOnlyList<TextSegment> SegmentsOf(string text)
    {
        var document = Markdig.Markdown.Parse(text, PIPELINE);
        var segments = new List<TextSegment>();
        foreach (var block in document.Descendants<LeafBlock>())
        {
            if (block is CodeBlock or LinkReferenceDefinition || block.Inline is not { FirstChild: not null } inline)
                continue;

            if (new InlineWalker(text).TryRead(inline) is { } segment)
                segments.Add(segment);
        }

        return segments;
    }

    /// <summary>
    /// Walks the inline parts of one block and puts its segment together.
    /// </summary>
    private sealed class InlineWalker(string text)
    {
        private readonly TextSegmentBuilder builder = new();
        private int position = -1;
        private bool afterSoftLineBreak;
        private int? hardLineBreakId;
        private bool isConsistent = true;

        /// <summary>
        /// Where the segment starts and ends, without the white space around its words.
        /// </summary>
        private int segmentStart;

        private int segmentEnd;

        public TextSegment? TryRead(ContainerInline root)
        {
            this.segmentStart = root.FirstChild!.Span.Start;
            this.segmentEnd = Math.Min(root.LastChild!.Span.End + 1, text.Length);
            while (this.segmentStart < this.segmentEnd && text[this.segmentStart] is ' ' or '\t')
                this.segmentStart++;

            while (this.segmentEnd > this.segmentStart && text[this.segmentEnd - 1] is ' ' or '\t')
                this.segmentEnd--;

            this.position = this.segmentStart;
            this.VisitChildren(root);

            return this.isConsistent ? this.builder.TryBuild(this.segmentStart, this.position) : null;
        }

        private void VisitChildren(ContainerInline container)
        {
            foreach (var child in container)
            {
                if (!this.isConsistent)
                    return;

                this.Visit(child);
            }
        }

        private void Visit(Inline inline)
        {
            var start = inline.Span.Start;
            var end = inline.Span.End + 1;

            // Plain words at the edges lose the white space around them, which stays outside of the segment:
            if (inline is LiteralInline { IsFirstCharacterEscaped: false } && !inline.Span.IsEmpty)
            {
                start = Math.Max(start, this.segmentStart);
                end = Math.Min(end, this.segmentEnd);
                if (end <= start)
                    return;
            }

            if (inline.Span.IsEmpty || start < this.position || end > text.Length)
            {
                this.isConsistent = false;
                return;
            }

            switch (inline)
            {
                case LineBreakInline { IsHard: false }:
                    // Spaces at the end of the line make no difference before the space which replaces the line break:
                    if (!string.IsNullOrWhiteSpace(text[this.position..start]))
                        this.AddGap(start);

                    this.builder.Words(" ");
                    this.position = end;
                    this.afterSoftLineBreak = true;
                    this.hardLineBreakId = null;
                    return;

                case LineBreakInline:
                    // Two spaces or a backslash before the line break belong to it:
                    var lineBreak = this.builder.Placeholder(text[this.position..end]);
                    this.position = end;
                    this.afterSoftLineBreak = false;
                    this.hardLineBreakId = lineBreak;
                    return;

                // Escaped characters and entities reach the model as they read, e.g., "|" for "\|" or "&" for "&amp;":
                case LiteralInline { IsFirstCharacterEscaped: true } escaped:
                    this.AddGap(start);
                    this.builder.Words(escaped.Content.ToString());
                    break;

                case HtmlEntityInline entity:
                    this.AddGap(start);
                    this.builder.Words(entity.Transcoded.ToString());
                    break;

                case LiteralInline:
                    this.AddGap(start);
                    this.builder.Words(text[start..end]);
                    break;

                case ContainerInline { FirstChild: { } firstChild } container when container is not LinkInline { IsAutoLink: true }:
                    this.AddGap(start);
                    var childrenStart = firstChild.Span.Start;
                    if (childrenStart < start)
                    {
                        this.isConsistent = false;
                        return;
                    }

                    var pair = this.builder.Open(text[start..childrenStart]);
                    this.position = childrenStart;
                    this.VisitChildren(container);
                    if (!this.isConsistent || this.position > end)
                    {
                        this.isConsistent = false;
                        return;
                    }

                    this.builder.Close(pair, text[this.position..end]);
                    break;

                default:
                    this.AddGap(start);
                    this.builder.Placeholder(text[start..end]);
                    break;
            }

            this.position = end;
            this.afterSoftLineBreak = false;
            this.hardLineBreakId = null;
        }

        /// <summary>
        /// Handles what lies between two parts: the indentation or quote marks at the start of a line, which
        /// belong to the line break before them, or words Markdig did not cover.
        /// </summary>
        private void AddGap(int start)
        {
            if (start <= this.position)
                return;

            var gap = text[this.position..start];
            if (this.hardLineBreakId is { } lineBreak)
                this.builder.ExtendPlaceholder(lineBreak, gap);
            else if (!this.afterSoftLineBreak)
                this.builder.Words(gap);
        }
    }
}