using System.Collections.Frozen;
using System.Text.RegularExpressions;

using HtmlAgilityPack;

namespace AIStudio.Tools.DocumentSegments.Html;

/// <summary>
/// Reads the segments of a web page: the words of its blocks, e.g., paragraphs, headings, list items, and table
/// cells, and the texts of the attributes people read, e.g., the alternative texts of pictures.
/// </summary>
/// <remarks>
/// <para>
/// The segments come from the positions HtmlAgilityPack reads for each part of the page, so the new page is the
/// source with the words of each segment replaced, and everything else stays byte for byte.
/// </para>
/// <para>
/// Elements which mark up words, e.g., links, emphasis, or spans, become pairs of tags, which stand for their start
/// and end tags, so a model moves them with their words and never touches an address. Code, pictures, line breaks,
/// form fields, scripts, and the like become tags of their own. Words go to the model with their entities decoded
/// and their white space collapsed, as a browser shows them.
/// </para>
/// </remarks>
internal static partial class HtmlSegmentReader
{
    /// <summary>
    /// Elements which mark up words within a block.
    /// </summary>
    private static readonly FrozenSet<string> INLINE_ELEMENTS = FrozenSet.Create(StringComparer.OrdinalIgnoreCase,
        "a", "abbr", "acronym", "b", "bdi", "bdo", "big", "button", "cite", "data", "del", "dfn", "em", "font", "i", "ins",
        "label", "mark", "nobr", "output", "q", "s", "small", "span", "strike", "strong", "sub", "sup", "time", "tt", "u");

    /// <summary>
    /// Elements within a block which stay as they are, with everything inside them.
    /// </summary>
    private static readonly FrozenSet<string> ATOMIC_ELEMENTS = FrozenSet.Create(StringComparer.OrdinalIgnoreCase,
        "audio", "br", "canvas", "code", "embed", "iframe", "img", "input", "kbd", "map", "math", "noscript", "object",
        "picture", "samp", "script", "select", "style", "svg", "template", "textarea", "var", "video", "wbr");

    /// <summary>
    /// Blocks which stay as they are, with everything inside them.
    /// </summary>
    private static readonly FrozenSet<string> SKIPPED_BLOCKS = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "pre");

    /// <summary>
    /// Elements whose attributes nobody reads, since they hold code or a graphic.
    /// </summary>
    private static readonly FrozenSet<string> UNREAD_ELEMENTS = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "math", "script", "style", "svg", "template");

    /// <summary>
    /// The attributes whose texts people read, e.g., as a tooltip or instead of a picture.
    /// </summary>
    private static readonly FrozenSet<string> READABLE_ATTRIBUTES = FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "alt", "aria-label", "placeholder", "title");

    /// <summary>
    /// The meta elements whose content describes the page, e.g., in search results or in a preview of a link.
    /// </summary>
    private static readonly FrozenSet<string> DESCRIBING_META_NAMES = FrozenSet.Create(StringComparer.OrdinalIgnoreCase,
        "description", "og:title", "og:description", "twitter:title", "twitter:description");

    /// <summary>
    /// White space as HTML knows it; a no-break space is none.
    /// </summary>
    private const string HTML_WHITE_SPACE = " \t\r\n\f";

    [GeneratedRegex("[ \t\r\n\f]+")]
    private static partial Regex WhiteSpaceRegex();

    /// <summary>
    /// Reads a page.
    /// </summary>
    /// <param name="text">The page.</param>
    public static HtmlDocument Parse(string text)
    {
        var document = new HtmlDocument();
        document.LoadHtml(text);
        return document;
    }

    /// <summary>
    /// Reads the segments of the blocks of a page, in the order of the page.
    /// </summary>
    /// <param name="text">The page.</param>
    public static List<TextSegment> BlockSegmentsOf(string text)
    {
        var segments = new List<TextSegment>();
        CollectBlock(Parse(text).DocumentNode, text, segments);
        return segments;
    }

    /// <summary>
    /// Reads the segments of the readable attributes of a page, in the order of the page.
    /// </summary>
    /// <param name="text">The page.</param>
    public static List<TextSegment> AttributeSegmentsOf(string text)
    {
        var segments = new List<TextSegment>();
        foreach (var element in Parse(text).DocumentNode.Descendants().Where(node => node.NodeType is HtmlNodeType.Element))
        {
            foreach (var attribute in element.Attributes)
            {
                if (!IsReadable(element, attribute) || attribute.ValueLength <= 0 || attribute.ValueStartIndex < 0 || attribute.ValueStartIndex + attribute.ValueLength > text.Length)
                    continue;

                var builder = new TextSegmentBuilder();
                builder.Words(Collapsed(text.Substring(attribute.ValueStartIndex, attribute.ValueLength)));
                var segment = builder.Build(attribute.ValueStartIndex, attribute.ValueStartIndex + attribute.ValueLength);
                if (!builder.HasTagLikeWords && segment.Segment.HasWords)
                    segments.Add(segment);
            }
        }

        segments.Sort((first, second) => first.Start.CompareTo(second.Start));
        return segments;
    }

    /// <summary>
    /// Makes words fit into the text of a page.
    /// </summary>
    public static string EncodedText(string words) => words.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>
    /// Makes words fit into the value of an attribute, whichever quotes enclose it.
    /// </summary>
    public static string EncodedAttribute(string words) => EncodedText(words).Replace("\"", "&quot;").Replace("'", "&#39;");

    private static bool IsReadable(HtmlNode element, HtmlAttribute attribute)
    {
        // Nothing inside a script, a style, or a graphic is read by people:
        if (UNREAD_ELEMENTS.Contains(element.Name) || element.Ancestors().Any(ancestor => UNREAD_ELEMENTS.Contains(ancestor.Name)))
            return false;

        if (READABLE_ATTRIBUTES.Contains(attribute.Name))
            return true;

        return element.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)
               && attribute.Name.Equals("content", StringComparison.OrdinalIgnoreCase)
               && (DESCRIBING_META_NAMES.Contains(element.GetAttributeValue("name", string.Empty)) || DESCRIBING_META_NAMES.Contains(element.GetAttributeValue("property", string.Empty)));
    }

    /// <summary>
    /// Collects the segments within a block: each run of words between its blocks is one segment.
    /// </summary>
    private static void CollectBlock(HtmlNode block, string text, List<TextSegment> segments)
    {
        var run = new List<HtmlNode>();
        foreach (var child in block.ChildNodes)
        {
            if (IsWithinWords(child))
            {
                run.Add(child);
                continue;
            }

            AddRun(run, text, segments);
            run.Clear();
            if (child.NodeType is HtmlNodeType.Element && !SKIPPED_BLOCKS.Contains(child.Name))
                CollectBlock(child, text, segments);
        }

        AddRun(run, text, segments);
    }

    private static bool IsWithinWords(HtmlNode node) => node.NodeType switch
    {
        HtmlNodeType.Text or HtmlNodeType.Comment => true,
        HtmlNodeType.Element => INLINE_ELEMENTS.Contains(node.Name) || ATOMIC_ELEMENTS.Contains(node.Name),

        _ => false,
    };

    private static void AddRun(List<HtmlNode> run, string text, List<TextSegment> segments)
    {
        // The white space around the words stays outside of the segment, e.g., the indentation of the source:
        var first = run.FindIndex(node => !IsBlank(node, text));
        if (first < 0)
            return;

        var last = run.FindLastIndex(node => !IsBlank(node, text));
        if (new InlineWalker(text).TryRead(run.GetRange(first, last - first + 1)) is { } segment)
            segments.Add(segment);
    }

    private static bool IsBlank(HtmlNode node, string text) => node.NodeType is HtmlNodeType.Text && OuterOf(node, text).AsSpan().Trim(HTML_WHITE_SPACE).IsEmpty;

    private static string OuterOf(HtmlNode node, string text) => node.OuterStartIndex >= 0 && node.OuterStartIndex + node.OuterLength <= text.Length
        ? text.Substring(node.OuterStartIndex, node.OuterLength)
        : string.Empty;

    /// <summary>
    /// Words as a browser shows them: entities decoded and white space collapsed.
    /// </summary>
    private static string Collapsed(string html) => WhiteSpaceRegex().Replace(HtmlEntity.DeEntitize(html), " ");

    /// <summary>
    /// Walks the nodes of one run of words and puts its segment together.
    /// </summary>
    private sealed class InlineWalker(string text)
    {
        private readonly TextSegmentBuilder builder = new();
        private int position;
        private bool isConsistent = true;

        public TextSegment? TryRead(List<HtmlNode> nodes)
        {
            var start = nodes[0].OuterStartIndex;
            if (nodes[0].NodeType is HtmlNodeType.Text)
            {
                var firstWords = OuterOf(nodes[0], text).AsSpan();
                start += firstWords.Length - firstWords.TrimStart(HTML_WHITE_SPACE).Length;
            }

            var end = nodes[^1].OuterStartIndex + nodes[^1].OuterLength;
            if (nodes[^1].NodeType is HtmlNodeType.Text)
            {
                var lastWords = OuterOf(nodes[^1], text).AsSpan();
                end -= lastWords.Length - lastWords.TrimEnd(HTML_WHITE_SPACE).Length;
            }

            if (start < 0 || end > text.Length || end <= start)
                return null;

            this.position = start;
            foreach (var node in nodes)
                this.Visit(node, end);

            if (!this.isConsistent || this.position != end || this.builder.HasTagLikeWords)
                return null;

            var segment = this.builder.Build(start, end);
            return segment.Segment.HasWords ? segment : null;
        }

        private void Visit(HtmlNode node, int limit)
        {
            if (!this.isConsistent)
                return;

            var outerStart = node.OuterStartIndex;
            var outerEnd = outerStart + node.OuterLength;
            if (outerStart < 0 || outerEnd > text.Length || !this.TakeGapUntil(outerStart, node))
            {
                this.isConsistent = false;
                return;
            }

            // The first words of a run start behind their leading white space, the last ones end before their trailing one:
            var nodeStart = Math.Max(outerStart, this.position);
            var nodeEnd = Math.Min(outerEnd, limit);
            if (nodeEnd < nodeStart)
            {
                this.isConsistent = false;
                return;
            }

            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    this.builder.Words(Collapsed(text[nodeStart..nodeEnd]));
                    break;

                case HtmlNodeType.Element when INLINE_ELEMENTS.Contains(node.Name) && node.HasChildNodes && node.InnerStartIndex > nodeStart && node.InnerStartIndex + node.InnerLength <= nodeEnd:
                    var innerEnd = node.InnerStartIndex + node.InnerLength;
                    var pair = this.builder.Open(text[nodeStart..node.InnerStartIndex]);
                    this.position = node.InnerStartIndex;
                    foreach (var child in node.ChildNodes)
                        this.Visit(child, innerEnd);

                    if (!this.isConsistent || this.position > innerEnd || !this.TakeGapUntil(innerEnd, null))
                    {
                        this.isConsistent = false;
                        return;
                    }

                    this.builder.Close(pair, text[innerEnd..nodeEnd]);
                    break;

                // Code, pictures, scripts, comments, and markup without words:
                default:
                    this.builder.Placeholder(text[nodeStart..nodeEnd]);
                    break;
            }

            this.position = nodeEnd;
        }

        /// <summary>
        /// Takes what lies between the last node and the next one, which may only be white space.
        /// </summary>
        /// <param name="next">Where the next node starts.</param>
        /// <param name="nextNode">The next node; null for the end of an element.</param>
        /// <returns>Whether the next node follows right away or after white space, rather than overlapping the last one or leaving markup in between.</returns>
        private bool TakeGapUntil(int next, HtmlNode? nextNode)
        {
            // Only the first words of a run start before it, by the white space left out:
            if (next < this.position)
                return nextNode is { NodeType: HtmlNodeType.Text };

            if (next == this.position)
                return true;

            if (!text.AsSpan(this.position, next - this.position).Trim(HTML_WHITE_SPACE).IsEmpty)
                return false;

            this.builder.Words(" ");
            this.position = next;
            return true;
        }
    }
}