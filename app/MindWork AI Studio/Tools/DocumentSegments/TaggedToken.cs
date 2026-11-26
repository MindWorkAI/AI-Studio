namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// One part of a tagged text: words, or a tag.
/// </summary>
/// <param name="Kind">What the part is.</param>
/// <param name="Id">The number of the tag; 0 for words.</param>
/// <param name="Text">The words; empty for a tag.</param>
public readonly record struct TaggedToken(TaggedTokenKind Kind, int Id, string Text)
{
    /// <summary>
    /// Creates a part with words.
    /// </summary>
    /// <param name="text">The words.</param>
    public static TaggedToken Words(string text) => new(TaggedTokenKind.TEXT, 0, text);

    /// <summary>
    /// Creates a tag.
    /// </summary>
    /// <param name="kind">The kind of tag.</param>
    /// <param name="id">The number of the tag.</param>
    public static TaggedToken Tag(TaggedTokenKind kind, int id) => new(kind, id, string.Empty);
}