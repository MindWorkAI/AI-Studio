namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// What became of a segment, and its new text.
/// </summary>
/// <param name="Status">What became of the segment.</param>
/// <param name="Text">The new text with tags, as the model wrote it; null unless the segment was transformed.</param>
public readonly record struct SegmentOutcome(SegmentStatus Status, string? Text)
{
    /// <summary>
    /// A segment a model worked on.
    /// </summary>
    /// <remarks>
    /// The tags of the text may still differ from those of the source, when the model missed them twice:
    /// the format then writes the words without the inline structure rather than nothing.
    /// </remarks>
    /// <param name="text">The new text with tags.</param>
    public static SegmentOutcome Transformed(string text) => new(SegmentStatus.TRANSFORMED, text);

    /// <summary>
    /// A segment which the prompt injection filter kept from the model.
    /// </summary>
    public static readonly SegmentOutcome FILTERED = new(SegmentStatus.FILTERED, null);

    /// <summary>
    /// A segment for which no model delivered a result.
    /// </summary>
    public static readonly SegmentOutcome FAILED = new(SegmentStatus.FAILED, null);
}