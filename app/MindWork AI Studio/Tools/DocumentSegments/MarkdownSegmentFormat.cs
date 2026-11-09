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
/// </remarks>
public sealed class MarkdownSegmentFormat : IDocumentSegmentFormat
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
    public FileTypeFilter FileType => FileTypes.MARKDOWN;

    /// <inheritdoc />
    public string LimitsNotice => TB("Code, formulas, link targets, and embedded HTML stay as they are.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(path, token);
        return SegmentsOf(file.Text).Select(segment => segment.Segment).ToList();
    }

    /// <inheritdoc />
    public async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(sourcePath, token);
        var result = TextSegmentWriter.Apply(file.Text, SegmentsOf(file.Text), results, WithoutLineBreaks, out var text);
        if (result.Outcome is SegmentWriteOutcome.WRITTEN)
            await file.WriteAsync(targetPath, text, token);

        return result;
    }

    /// <summary>
    /// Reads the segments of a Markdown text, in the order of the text.
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

        // Footnotes come last in the tree of Markdig, wherever they stand in the text:
        segments.Sort((first, second) => first.Start.CompareTo(second.Start));
        return segments;
    }

    /// <summary>
    /// Keeps a paragraph in one piece: a line break inside it would end it, or a table row.
    /// </summary>
    private static string WithoutLineBreaks(string words) => words.ReplaceLineEndings(" ");

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

        public TextSegment? TryRead(ContainerInline root)
        {
            this.position = root.FirstChild!.Span.Start;
            var start = this.position;
            this.VisitChildren(root);

            if (!this.isConsistent || this.builder.HasTagLikeWords)
                return null;

            return this.builder.Build(start, this.position);
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

                case LiteralInline or HtmlEntityInline:
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