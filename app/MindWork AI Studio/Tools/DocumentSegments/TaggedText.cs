using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The text of a segment together with its inline structure, written as numbered tags which a model keeps
/// while it works on the words around them.
/// </summary>
/// <remarks>
/// A pair of tags, such as &lt;1&gt;…&lt;/1&gt;, encloses words which belong to something of the document,
/// e.g., a link or a bold run. A tag of its own, such as &lt;2/&gt;, stands for something without words,
/// e.g., a tab, a picture, or the reference to a footnote. Only the format which read the segment knows what
/// a number stands for; it puts that back when it writes the result. A model may move the tags along with
/// the words, but each pair has to stay within the same pair as in the source, so that the format can nest
/// its parts again as before. Tags of their own may go anywhere.
/// </remarks>
public sealed class TaggedText
{
    /// <summary>
    /// The longest number of a tag, in digits, so that a long number in a text is never taken for one.
    /// </summary>
    private const int MAX_ID_DIGITS = 6;

    private string? text;

    /// <summary>
    /// Creates a tagged text from its parts.
    /// </summary>
    /// <remarks>
    /// Use TaggedTextBuilder to put a text together, which numbers the tags and merges adjacent words.
    /// </remarks>
    /// <param name="tokens">The parts, in their order.</param>
    public TaggedText(IReadOnlyList<TaggedToken> tokens) => this.Tokens = tokens;

    /// <summary>
    /// The parts of the text, in their order.
    /// </summary>
    public IReadOnlyList<TaggedToken> Tokens { get; }

    /// <summary>
    /// The words of the text without any tag.
    /// </summary>
    public string PlainText => string.Concat(this.Tokens.Where(token => token.Kind is TaggedTokenKind.TEXT).Select(token => token.Text));

    /// <summary>
    /// Whether the text has any letter, i.e., whether a model has anything to work on.
    /// </summary>
    public bool HasLetters => this.Tokens.Any(token => token.Kind is TaggedTokenKind.TEXT && token.Text.Any(char.IsLetter));

    /// <summary>
    /// Whether the text has any tag.
    /// </summary>
    public bool HasTags => this.Tokens.Any(token => token.Kind is not TaggedTokenKind.TEXT);

    /// <summary>
    /// Writes the text with its tags, as a model gets to see it.
    /// </summary>
    public override string ToString()
    {
        if (this.text is not null)
            return this.text;

        var builder = new StringBuilder();
        foreach (var token in this.Tokens)
        {
            switch (token.Kind)
            {
                case TaggedTokenKind.TEXT:
                    builder.Append(token.Text);
                    break;

                case TaggedTokenKind.OPEN:
                    builder.Append('<').Append(token.Id.ToString(CultureInfo.InvariantCulture)).Append('>');
                    break;

                case TaggedTokenKind.CLOSE:
                    builder.Append("</").Append(token.Id.ToString(CultureInfo.InvariantCulture)).Append('>');
                    break;

                case TaggedTokenKind.PLACEHOLDER:
                    builder.Append('<').Append(token.Id.ToString(CultureInfo.InvariantCulture)).Append("/>");
                    break;
            }
        }

        this.text = builder.ToString();
        return this.text;
    }

    /// <summary>
    /// Reads a text with tags, e.g., what a model wrote.
    /// </summary>
    /// <remarks>
    /// Only &lt;1&gt;, &lt;/1&gt;, and &lt;1/&gt; are tags, with a number without leading zeros. Anything
    /// else in angle brackets stays words. Whether the tags match those of the source tells HasSameTagsAs.
    /// </remarks>
    /// <param name="value">The text with tags.</param>
    /// <param name="taggedText">The text, or null when its tags do not fit together.</param>
    /// <param name="issue">Why the tags do not fit together, written for the model; empty on success.</param>
    /// <returns>Whether the tags fit together: every pair is closed in the right order, and no number appears twice.</returns>
    public static bool TryRead(string value, [NotNullWhen(true)] out TaggedText? taggedText, out string issue)
    {
        taggedText = null;
        issue = string.Empty;

        var builder = new TaggedTextBuilder();
        var seenIds = new HashSet<int>();
        var openPairs = new Stack<int>();
        var wordsStart = 0;
        var index = 0;
        while (index < value.Length)
        {
            if (!TryMatchTag(value, index, out var kind, out var id, out var length))
            {
                index++;
                continue;
            }

            builder.Words(value[wordsStart..index]);
            switch (kind)
            {
                case TaggedTokenKind.OPEN when !seenIds.Add(id):
                case TaggedTokenKind.PLACEHOLDER when !seenIds.Add(id):
                    issue = $"The number {id} is used by more than one tag; every tag appears exactly once.";
                    return false;

                case TaggedTokenKind.OPEN:
                    openPairs.Push(id);
                    break;

                case TaggedTokenKind.CLOSE when openPairs.Count == 0 || openPairs.Peek() != id:
                    issue = seenIds.Contains(id)
                        ? $"The closing tag </{id}> does not close the pair opened last; pairs must not overlap."
                        : $"The closing tag </{id}> has no opening tag <{id}> before it.";
                    return false;

                case TaggedTokenKind.CLOSE:
                    openPairs.Pop();
                    break;
            }

            builder.Tag(kind, id);
            index += length;
            wordsStart = index;
        }

        if (openPairs.Count > 0)
        {
            issue = $"The tag <{openPairs.Peek()}> is never closed.";
            return false;
        }

        builder.Words(value[wordsStart..]);
        taggedText = builder.Build();
        return true;
    }

    /// <summary>
    /// Checks whether this text, e.g., the work of a model, has the same tags as its source.
    /// </summary>
    /// <param name="source">The text the model worked on.</param>
    /// <param name="issue">What differs, written for the model; empty when nothing does.</param>
    /// <returns>Whether every tag of the source appears here, no other tag does, and every pair stays within the same pair.</returns>
    public bool HasSameTagsAs(TaggedText source, out string issue)
    {
        var issues = new List<string>();
        var ownPairs = ParentsOfPairs(this);
        var sourcePairs = ParentsOfPairs(source);
        var ownPlaceholders = this.Tokens.Where(token => token.Kind is TaggedTokenKind.PLACEHOLDER).Select(token => token.Id).ToHashSet();
        var sourcePlaceholders = source.Tokens.Where(token => token.Kind is TaggedTokenKind.PLACEHOLDER).Select(token => token.Id).ToHashSet();

        foreach (var (id, parent) in sourcePairs)
        {
            if (!ownPairs.TryGetValue(id, out var ownParent))
                issues.Add($"The pair <{id}>…</{id}> is missing.");
            else if (ownParent != parent)
                issues.Add(parent == 0
                    ? $"The pair <{id}>…</{id}> must not be inside another pair."
                    : $"The pair <{id}>…</{id}> must stay inside the pair <{parent}>…</{parent}>.");
        }

        issues.AddRange(ownPairs.Keys.Where(id => !sourcePairs.ContainsKey(id)).Select(id => $"The pair <{id}>…</{id}> does not belong to the text."));
        issues.AddRange(sourcePlaceholders.Where(id => !ownPlaceholders.Contains(id)).Select(id => $"The tag <{id}/> is missing."));
        issues.AddRange(ownPlaceholders.Where(id => !sourcePlaceholders.Contains(id)).Select(id => $"The tag <{id}/> does not belong to the text."));

        issue = string.Join(" ", issues);
        return issues.Count == 0;
    }

    /// <summary>
    /// Whether a text holds something which TryRead would take for a tag.
    /// </summary>
    /// <remarks>
    /// For formats which put words of a document into a tagged text: such words could not be told apart from
    /// the tags of the format, so the format leaves out its tags for this segment.
    /// </remarks>
    /// <param name="value">The words of the document.</param>
    public static bool ContainsTagLikeText(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (TryMatchTag(value, index, out _, out _, out _))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes everything which reads as a tag, e.g., from the work of a model whose tags do not fit.
    /// </summary>
    /// <param name="value">The text with tags.</param>
    /// <returns>The words alone.</returns>
    public static string RemoveTags(string value)
    {
        var builder = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            if (TryMatchTag(value, index, out _, out _, out var length))
            {
                index += length;
                continue;
            }

            builder.Append(value[index]);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Finds for every pair the pair it lies in; 0 for none.
    /// </summary>
    private static Dictionary<int, int> ParentsOfPairs(TaggedText taggedText)
    {
        var parents = new Dictionary<int, int>();
        var openPairs = new Stack<int>();
        foreach (var token in taggedText.Tokens)
        {
            switch (token.Kind)
            {
                case TaggedTokenKind.OPEN:
                    parents.TryAdd(token.Id, openPairs.Count == 0 ? 0 : openPairs.Peek());
                    openPairs.Push(token.Id);
                    break;

                case TaggedTokenKind.CLOSE when openPairs.Count > 0:
                    openPairs.Pop();
                    break;
            }
        }

        return parents;
    }

    /// <summary>
    /// Matches &lt;1&gt;, &lt;/1&gt;, or &lt;1/&gt; at the given position.
    /// </summary>
    private static bool TryMatchTag(string value, int index, out TaggedTokenKind kind, out int id, out int length)
    {
        kind = TaggedTokenKind.TEXT;
        id = 0;
        length = 0;
        if (value[index] != '<')
            return false;

        var position = index + 1;
        var isClosing = position < value.Length && value[position] == '/';
        if (isClosing)
            position++;

        var digitsStart = position;
        while (position < value.Length && char.IsAsciiDigit(value[position]))
            position++;

        var digits = position - digitsStart;
        if (digits is 0 or > MAX_ID_DIGITS || value[digitsStart] == '0')
            return false;

        var isPlaceholder = !isClosing && position < value.Length && value[position] == '/';
        if (isPlaceholder)
            position++;

        if (position >= value.Length || value[position] != '>')
            return false;

        id = int.Parse(value.AsSpan(digitsStart, digits), NumberStyles.None, CultureInfo.InvariantCulture);
        kind = isClosing ? TaggedTokenKind.CLOSE : isPlaceholder ? TaggedTokenKind.PLACEHOLDER : TaggedTokenKind.OPEN;
        length = position + 1 - index;
        return true;
    }
}