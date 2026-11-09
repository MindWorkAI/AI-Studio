namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Puts a segment of a document which is text itself together, while a format walks its markup.
/// </summary>
public sealed class TextSegmentBuilder
{
    private readonly TaggedTextBuilder text = new();
    private readonly Dictionary<int, string> openings = [];
    private readonly Dictionary<int, string> closings = [];
    private readonly Dictionary<int, string> placeholders = [];

    /// <summary>
    /// Whether any words added so far read as a tag, see TaggedText.ContainsTagLikeText.
    /// </summary>
    public bool HasTagLikeWords { get; private set; }

    /// <summary>
    /// Adds words, which a model works on.
    /// </summary>
    /// <param name="words">The words, as the source holds them.</param>
    public void Words(string words)
    {
        this.HasTagLikeWords |= TaggedText.ContainsTagLikeText(words);
        this.text.Words(words);
    }

    /// <summary>
    /// Opens a pair of tags for markup which encloses words, e.g., the start of a link.
    /// </summary>
    /// <param name="markup">The markup before the words.</param>
    /// <returns>The number of the pair.</returns>
    public int Open(string markup)
    {
        var id = this.text.Open();
        this.openings[id] = markup;
        return id;
    }

    /// <summary>
    /// Closes a pair of tags.
    /// </summary>
    /// <param name="id">The number Open returned.</param>
    /// <param name="markup">The markup after the words, e.g., the end of a link with its address.</param>
    public void Close(int id, string markup)
    {
        this.text.Close(id);
        this.closings[id] = markup;
    }

    /// <summary>
    /// Adds a tag of its own for markup which the model keeps as it is, e.g., a code span.
    /// </summary>
    /// <param name="markup">The markup.</param>
    /// <returns>The number of the tag.</returns>
    public int Placeholder(string markup)
    {
        var id = this.text.Placeholder();
        this.placeholders[id] = markup;
        return id;
    }

    /// <summary>
    /// Adds markup to a tag of its own, e.g., the indentation of the line after a line break.
    /// </summary>
    /// <param name="id">The number Placeholder returned.</param>
    /// <param name="markup">The markup which follows.</param>
    public void ExtendPlaceholder(int id, string markup) => this.placeholders[id] += markup;

    /// <summary>
    /// Finishes the segment.
    /// </summary>
    /// <param name="start">Where the segment starts in the source.</param>
    /// <param name="end">Where the segment ends in the source, exclusive.</param>
    public TextSegment Build(int start, int end) => new(start, end, new DocumentSegment(this.text.Build()), this.openings, this.closings, this.placeholders);
}