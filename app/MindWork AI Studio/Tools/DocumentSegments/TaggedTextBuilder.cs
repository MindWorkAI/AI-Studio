namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Puts a tagged text together, e.g., while a format reads a paragraph.
/// </summary>
/// <remarks>
/// It numbers the tags from 1 in the order they are added and merges adjacent words, so that a model never
/// sees a text split for no reason.
/// </remarks>
public sealed class TaggedTextBuilder
{
    private readonly List<TaggedToken> tokens = [];
    private int lastId;

    /// <summary>
    /// Adds words.
    /// </summary>
    /// <param name="text">The words; nothing happens for an empty text.</param>
    public TaggedTextBuilder Words(string text)
    {
        if (text.Length == 0)
            return this;

        if (this.tokens.Count > 0 && this.tokens[^1].Kind is TaggedTokenKind.TEXT)
            this.tokens[^1] = TaggedToken.Words(this.tokens[^1].Text + text);
        else
            this.tokens.Add(TaggedToken.Words(text));

        return this;
    }

    /// <summary>
    /// Opens a pair of tags.
    /// </summary>
    /// <returns>The number of the pair, which Close needs.</returns>
    public int Open()
    {
        var id = ++this.lastId;
        this.tokens.Add(TaggedToken.Tag(TaggedTokenKind.OPEN, id));
        return id;
    }

    /// <summary>
    /// Closes a pair of tags.
    /// </summary>
    /// <param name="id">The number Open returned.</param>
    public TaggedTextBuilder Close(int id)
    {
        this.tokens.Add(TaggedToken.Tag(TaggedTokenKind.CLOSE, id));
        return this;
    }

    /// <summary>
    /// Adds a tag of its own, which stands for something without words.
    /// </summary>
    /// <returns>The number of the tag.</returns>
    public int Placeholder()
    {
        var id = ++this.lastId;
        this.tokens.Add(TaggedToken.Tag(TaggedTokenKind.PLACEHOLDER, id));
        return id;
    }

    /// <summary>
    /// Adds a tag with a number read elsewhere, e.g., from the answer of a model.
    /// </summary>
    /// <param name="kind">The kind of tag.</param>
    /// <param name="id">The number of the tag.</param>
    internal TaggedTextBuilder Tag(TaggedTokenKind kind, int id)
    {
        this.tokens.Add(TaggedToken.Tag(kind, id));
        this.lastId = Math.Max(this.lastId, id);
        return this;
    }

    /// <summary>
    /// Finishes the text.
    /// </summary>
    public TaggedText Build() => new(this.tokens.ToArray());
}