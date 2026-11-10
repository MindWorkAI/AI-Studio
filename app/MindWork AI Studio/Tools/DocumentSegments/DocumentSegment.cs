namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// One piece of a document which a model works on on its own, e.g., a paragraph, a heading, a table cell,
/// or the text of an attribute, with its inline structure as tags.
/// </summary>
/// <remarks>
/// Segments with the same text are worked on once: the results are found by Key. A format reads the
/// segments in the order of the document, so that a model gets them in the order a reader would.
/// </remarks>
/// <param name="Source">The text of the segment, as the document holds it.</param>
public sealed record DocumentSegment(TaggedText Source)
{
    /// <summary>
    /// What the result of this segment is found by: its text with tags.
    /// </summary>
    public string Key => this.Source.ToString();

    /// <summary>
    /// Whether a model has anything to work on, i.e., whether the segment has any letter.
    /// </summary>
    /// <remarks>
    /// A segment without letters, such as a number or a date, stays as it is and needs no result.
    /// </remarks>
    public bool HasWords => this.Source.HasLetters;
}