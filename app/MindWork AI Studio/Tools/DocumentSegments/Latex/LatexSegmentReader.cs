using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// Reads the segments of a LaTeX document: its paragraphs, the texts of its headings and captions, the labels of its
/// list items, the cells of its tables, and its title.
/// </summary>
/// <remarks>
/// <para>
/// The segments come from the positions LatexParser reads for each part of the document, so the new document is the
/// source with the words of each segment replaced, and everything else stays byte for byte. Only the body is read,
/// apart from the title and subtitle in the preamble; a document without \begin{document}, e.g., a chapter which
/// another document includes, is a body as a whole.
/// </para>
/// <para>
/// Commands which mark up words, e.g., \textbf, \footnote, or \href, and groups in braces become pairs of tags, which
/// stand for their markup. Formulas, references, citations, code, and every command the reader does not know become
/// tags of their own. Layout, e.g., \label, \centering, \hline, or a comment, stays outside of a segment at its
/// edges, so that a model cannot move it into the words.
/// </para>
/// </remarks>
internal sealed partial class LatexSegmentReader
{
    /// <summary>
    /// The commands of the preamble whose texts a reader sees.
    /// </summary>
    private static readonly FrozenSet<string> PREAMBLE_HEADINGS = FrozenSet.Create(StringComparer.Ordinal, "title", "subtitle");

    private const string BEGIN_DOCUMENT = @"\begin{document}";
    private const string END_DOCUMENT = @"\end{document}";

    /// <summary>
    /// Markup which ends in a command with letters, e.g., \today: TeX would take letters right after it for part of
    /// its name and drop a space after it.
    /// </summary>
    [GeneratedRegex(@"\\[A-Za-z]+\*?\z")]
    private static partial Regex EndsWithControlWordRegex();

    private readonly string text;
    private readonly List<TextSegment> segments = [];

    private LatexSegmentReader(string text) => this.text = text;

    /// <summary>
    /// Reads the segments of a document, in the order of the document.
    /// </summary>
    /// <param name="text">The document.</param>
    public static List<TextSegment> SegmentsOf(string text)
    {
        var reader = new LatexSegmentReader(text);
        var (preambleEnd, bodyStart, bodyEnd) = RegionsOf(text);
        foreach (var node in LatexParser.Parse(text, 0, preambleEnd))
        {
            if (node.Kind is LatexNodeKind.COMMAND && PREAMBLE_HEADINGS.Contains(node.Name))
                reader.AddHeading(node);
        }

        reader.CollectBlocks(LatexParser.Parse(text, bodyStart, bodyEnd), isTable: false);
        reader.segments.Sort((first, second) => first.Start.CompareTo(second.Start));
        return reader.segments;
    }

    /// <summary>
    /// Finds the preamble and the body of a document.
    /// </summary>
    private static (int PreambleEnd, int BodyStart, int BodyEnd) RegionsOf(string text)
    {
        var begin = IndexOutsideOfComments(text, BEGIN_DOCUMENT, 0);
        if (begin < 0)
            return (0, 0, text.Length);

        var bodyStart = begin + BEGIN_DOCUMENT.Length;
        var end = IndexOutsideOfComments(text, END_DOCUMENT, bodyStart);
        return (begin, bodyStart, end < 0 ? text.Length : end);
    }

    private static int IndexOutsideOfComments(string text, string value, int from)
    {
        var index = text.IndexOf(value, from, StringComparison.Ordinal);
        while (index >= 0 && IsInComment(text, index))
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);

        return index;
    }

    private static bool IsInComment(string text, int index)
    {
        var lineStart = index == 0 ? 0 : text.LastIndexOfAny(['\r', '\n'], index - 1) + 1;
        for (var position = lineStart; position < index; position++)
        {
            if (text[position] != '%')
                continue;

            // A percent sign after an odd number of backslashes is escaped:
            var backslashes = 0;
            while (position - backslashes - 1 >= lineStart && text[position - backslashes - 1] == '\\')
                backslashes++;

            if (backslashes % 2 == 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Collects the segments of nodes which may hold paragraphs: the body, an environment, or a group with paragraphs.
    /// </summary>
    private void CollectBlocks(IReadOnlyList<LatexNode> nodes, bool isTable)
    {
        var items = new List<Item>();
        foreach (var node in nodes)
        {
            if (!this.TryAddBlock(node, items, isTable))
                this.AddInline(node, items, isTable);
        }

        this.Flush(items, isInBrackets: false);
    }

    /// <summary>
    /// Handles a node which ends the paragraph before it, e.g., a blank line, a heading, or an environment.
    /// </summary>
    /// <returns>Whether the node ends the paragraph; otherwise, it belongs to the words.</returns>
    private bool TryAddBlock(LatexNode node, List<Item> items, bool isTable)
    {
        switch (node.Kind)
        {
            case LatexNodeKind.PARAGRAPH_BREAK:
            case LatexNodeKind.SPECIAL when isTable && node.Name == "&":
                this.Flush(items, isInBrackets: false);
                return true;

            case LatexNodeKind.GROUP when ContainsBlocks(node.Children, isTable):
                this.Flush(items, isInBrackets: false);
                this.CollectBlocks(node.Children, isTable);
                return true;

            case LatexNodeKind.ENVIRONMENT when LatexCommands.EnvironmentOf(node.Name) is not LatexEnvironmentRole.MATH:
                this.Flush(items, isInBrackets: false);
                this.AddEnvironment(node);
                return true;

            case LatexNodeKind.COMMAND:
                return this.TryAddBlockCommand(node, items, isTable);

            default:
                return false;
        }
    }

    private void AddEnvironment(LatexNode environment)
    {
        var role = LatexCommands.EnvironmentOf(environment.Name);
        if (role is LatexEnvironmentRole.VERBATIM)
            return;

        foreach (var argument in environment.Arguments)
        {
            if ((role is LatexEnvironmentRole.CONTAINER_WITH_TITLE && !argument.IsOptional) || (role is LatexEnvironmentRole.CONTAINER_WITH_NAME && argument.IsOptional))
                this.AddArgument(argument, argument.IsOptional);
        }

        this.CollectBlocks(environment.Children, role is LatexEnvironmentRole.TABLE);
    }

    private bool TryAddBlockCommand(LatexNode node, List<Item> items, bool isTable)
    {
        var command = LatexCommands.Of(node.Name);
        switch (command.Role)
        {
            case LatexCommandRole.HEADING:
                this.Flush(items, isInBrackets: false);
                this.AddHeading(node);
                return true;

            case LatexCommandRole.ITEM:
                this.Flush(items, isInBrackets: false);
                foreach (var label in node.Arguments.Where(argument => argument.IsOptional))
                    this.AddArgument(label, isInBrackets: true);

                return true;

            case LatexCommandRole.PARAGRAPH:
            case LatexCommandRole.LINE_BREAK when isTable:
                this.Flush(items, isInBrackets: false);
                return true;

            case LatexCommandRole.WORDS when TextArgumentOf(node, command) is { } argument && ContainsBlocks(argument.Children, isTable):
                this.Flush(items, isInBrackets: false);
                this.CollectBlocks(argument.Children, isTable);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Adds the segments of a heading or a caption: its text and its optional arguments, e.g., a short title.
    /// </summary>
    private void AddHeading(LatexNode heading)
    {
        var command = LatexCommands.Of(heading.Name);
        var mandatoryIndex = 0;
        foreach (var argument in heading.Arguments)
        {
            if (argument.IsOptional)
                this.AddArgument(argument, isInBrackets: true);
            else if (mandatoryIndex++ == command.TextArgument)
                this.AddArgument(argument, isInBrackets: false);
        }
    }

    /// <summary>
    /// Adds the segment of an argument which holds words, or the segments of its paragraphs.
    /// </summary>
    private void AddArgument(LatexArgument argument, bool isInBrackets)
    {
        if (ContainsBlocks(argument.Children, isTable: false))
        {
            this.CollectBlocks(argument.Children, isTable: false);
            return;
        }

        var items = new List<Item>();
        foreach (var child in argument.Children)
            this.AddInline(child, items, isTable: false);

        this.Flush(items, isInBrackets);
    }

    /// <summary>
    /// Adds a node within the words of a paragraph.
    /// </summary>
    private void AddInline(LatexNode node, List<Item> items, bool isTable)
    {
        switch (node.Kind)
        {
            case LatexNodeKind.TEXT:
                this.AddText(node, items);
                break;

            case LatexNodeKind.ESCAPED_WORDS:
                items.Add(new WordsItem(node.Start, node.End, node.Words));
                break;

            case LatexNodeKind.SPECIAL when node.Name == "~":
                items.Add(new WordsItem(node.Start, node.End, LatexWords.NO_BREAK_SPACE));
                break;

            case LatexNodeKind.COMMENT:
                items.Add(this.Comment(node));
                break;

            case LatexNodeKind.MATH or LatexNodeKind.VERBATIM:
            case LatexNodeKind.SPECIAL when node.Name != "&":
            case LatexNodeKind.ENVIRONMENT when LatexCommands.EnvironmentOf(node.Name) is LatexEnvironmentRole.MATH:
                items.Add(this.Placeholder(node, isLayout: false));
                break;

            case LatexNodeKind.GROUP:
                items.Add(this.Pair(node.Start, node.End, node.Children, isTable));
                break;

            case LatexNodeKind.COMMAND:
                items.Add(this.InlineCommand(node, isTable));
                break;

            // An ampersand outside of a table, a block within words, or markup the parser could not follow:
            default:
                items.Add(new InvalidItem(node.Start, node.End));
                break;
        }
    }

    private Item InlineCommand(LatexNode node, bool isTable)
    {
        var command = LatexCommands.Of(node.Name);
        return command.Role switch
        {
            LatexCommandRole.WORDS when TextArgumentOf(node, command) is { } argument => this.Pair(node.Start, node.End, argument.Children, isTable),
            LatexCommandRole.LAYOUT or LatexCommandRole.LINE_BREAK => this.Placeholder(node, isLayout: true),
            LatexCommandRole.WORDS or LatexCommandRole.INLINE => this.Placeholder(node, isLayout: false),

            _ => new InvalidItem(node.Start, node.End),
        };
    }

    private PairItem Pair(int start, int end, IReadOnlyList<LatexNode> nodes, bool isTable)
    {
        var children = new List<Item>();
        foreach (var child in nodes)
            this.AddInline(child, children, isTable);

        return new PairItem(start, end, children);
    }

    /// <summary>
    /// Splits words at their white space, so that the white space at the edges of a segment stays outside of it.
    /// </summary>
    private void AddText(LatexNode node, List<Item> items)
    {
        var index = node.Start;
        while (index < node.End)
        {
            var isSpace = IsWhiteSpace(this.text[index]);
            var end = index + 1;
            while (end < node.End && IsWhiteSpace(this.text[end]) == isSpace)
                end++;

            items.Add(isSpace ? new SpaceItem(index, end) : new WordsItem(index, end, this.text[index..end]));
            index = end;
        }
    }

    private static bool IsWhiteSpace(char character) => character is ' ' or '\t' or '\r' or '\n';

    /// <summary>
    /// A comment ends with its line, and TeX skips the spaces at the start of the next one. Its markup starts with a
    /// space when the source has one before it, so that words placed right before it stay apart from the words after it.
    /// </summary>
    private PlaceholderItem Comment(LatexNode node)
    {
        var markup = this.text[node.Start..node.End];
        if (node.Start > 0 && IsWhiteSpace(this.text[node.Start - 1]))
            markup = " " + markup;

        return new PlaceholderItem(node.Start, node.End, markup, IsLayout: true);
    }

    private PlaceholderItem Placeholder(LatexNode node, bool isLayout) => new(node.Start, node.End, this.text[node.Start..node.End], isLayout);

    /// <summary>
    /// Finishes the segment of a paragraph, if it has any words.
    /// </summary>
    private void Flush(List<Item> items, bool isInBrackets)
    {
        if (this.Build(items, isInBrackets) is { } segment)
            this.segments.Add(segment);

        items.Clear();
    }

    private TextSegment? Build(List<Item> items, bool isInBrackets)
    {
        var first = items.FindIndex(IsContent);
        if (first < 0 || items.Any(IsInvalid))
            return null;

        var last = items.FindLastIndex(IsContent);
        var builder = new TextSegmentBuilder();
        this.Emit(items, first, last, builder);

        var segment = builder.Build(items[first].Start, items[last].End, isInBrackets ? LatexWords.EncodedInBrackets : null);
        return builder.HasTagLikeWords || !segment.Segment.HasWords ? null : segment;
    }

    /// <summary>
    /// Puts items together into a segment; markup right next to markup becomes one tag, e.g., ~\ref{a}\cite{b}.
    /// </summary>
    private void Emit(List<Item> items, int first, int last, TextSegmentBuilder builder)
    {
        var markup = new StringBuilder();
        for (var index = first; index <= last; index++)
        {
            var item = items[index];
            if (this.PlaceholderMarkupOf(item) is { } placeholder)
            {
                markup.Append(placeholder);
                continue;
            }

            AddPlaceholder(markup, builder);
            switch (item)
            {
                case WordsItem words:
                    builder.Words(words.Words);
                    break;

                case SpaceItem:
                    builder.Words(" ");
                    break;

                case PairItem pair:
                    this.EmitPair(pair, builder);
                    break;
            }
        }

        AddPlaceholder(markup, builder);
    }

    /// <summary>
    /// Writes a pair, whose layout and white space at its edges belong to its markup, e.g., \bfseries of {\bfseries words}.
    /// </summary>
    private void EmitPair(PairItem pair, TextSegmentBuilder builder)
    {
        var first = pair.Children.FindIndex(IsContent);
        var last = pair.Children.FindLastIndex(IsContent);
        var id = builder.Open(KeptApart(this.text[pair.Start..pair.Children[first].Start]));
        this.Emit(pair.Children, first, last, builder);
        builder.Close(id, this.text[pair.Children[last].End..pair.End]);
    }

    private static void AddPlaceholder(StringBuilder markup, TextSegmentBuilder builder)
    {
        if (markup.Length == 0)
            return;

        builder.Placeholder(KeptApart(markup.ToString()));
        markup.Clear();
    }

    /// <summary>
    /// Ends markup which ends in a command with letters with an empty group, so that new words after it, with or
    /// without a space, stay words of their own. Unchanged segments keep the source as it is.
    /// </summary>
    private static string KeptApart(string markup) => EndsWithControlWordRegex().IsMatch(markup) ? markup + "{}" : markup;

    /// <summary>
    /// The markup of an item which becomes a tag of its own: a placeholder, or a pair without letters, e.g., \textbf{42}.
    /// </summary>
    private string? PlaceholderMarkupOf(Item item) => item switch
    {
        PlaceholderItem placeholder => placeholder.Markup,
        PairItem pair when !HasLetters(pair) => this.text[pair.Start..pair.End],

        _ => null,
    };

    private static bool IsContent(Item item) => item is not (SpaceItem or PlaceholderItem { IsLayout: true });

    private static bool IsInvalid(Item item) => item is InvalidItem || (item is PairItem pair && pair.Children.Any(IsInvalid));

    private static bool HasLetters(Item item) => item switch
    {
        WordsItem words => words.Words.Any(char.IsLetter),
        PairItem pair => pair.Children.Any(HasLetters),

        _ => false,
    };

    /// <summary>
    /// Whether nodes hold something which ends a paragraph, so that they cannot be the words of a pair.
    /// </summary>
    private static bool ContainsBlocks(IReadOnlyList<LatexNode> nodes, bool isTable) => nodes.Any(node => node.Kind switch
    {
        LatexNodeKind.PARAGRAPH_BREAK => true,
        LatexNodeKind.SPECIAL => isTable && node.Name == "&",
        LatexNodeKind.GROUP => ContainsBlocks(node.Children, isTable),
        LatexNodeKind.ENVIRONMENT => LatexCommands.EnvironmentOf(node.Name) is not LatexEnvironmentRole.MATH,
        LatexNodeKind.COMMAND => IsBlockCommand(node, isTable),

        _ => false,
    });

    private static bool IsBlockCommand(LatexNode node, bool isTable)
    {
        var command = LatexCommands.Of(node.Name);
        return command.Role switch
        {
            LatexCommandRole.HEADING or LatexCommandRole.ITEM or LatexCommandRole.PARAGRAPH => true,
            LatexCommandRole.LINE_BREAK => isTable,
            LatexCommandRole.WORDS => TextArgumentOf(node, command) is { } argument && ContainsBlocks(argument.Children, isTable),

            _ => false,
        };
    }

    /// <summary>
    /// Finds the argument in braces which holds the words of a command.
    /// </summary>
    private static LatexArgument? TextArgumentOf(LatexNode node, LatexCommand command)
    {
        var mandatoryIndex = 0;
        foreach (var argument in node.Arguments)
        {
            if (argument.IsOptional)
                continue;

            if (mandatoryIndex++ == command.TextArgument)
                return argument;
        }

        return null;
    }

    /// <summary>
    /// A part of a paragraph, with where it lies in the source.
    /// </summary>
    private abstract record Item(int Start, int End);

    /// <summary>
    /// Words, as a model gets them.
    /// </summary>
    private sealed record WordsItem(int Start, int End, string Words) : Item(Start, End);

    /// <summary>
    /// White space, which a model gets as one space.
    /// </summary>
    private sealed record SpaceItem(int Start, int End) : Item(Start, End);

    /// <summary>
    /// Markup which becomes a tag of its own; layout stays outside of a segment at its edges.
    /// </summary>
    private sealed record PlaceholderItem(int Start, int End, string Markup, bool IsLayout) : Item(Start, End);

    /// <summary>
    /// Markup around words, e.g., \textbf{...} or a group.
    /// </summary>
    private sealed record PairItem(int Start, int End, List<Item> Children) : Item(Start, End);

    /// <summary>
    /// Markup the reader cannot make words of, which leaves its paragraph as it is.
    /// </summary>
    private sealed record InvalidItem(int Start, int End) : Item(Start, End);
}