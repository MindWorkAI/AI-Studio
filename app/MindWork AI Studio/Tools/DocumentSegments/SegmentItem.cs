namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The text of one segment in a batch, as a model gets it and answers it.
/// </summary>
public sealed class SegmentItem
{
    /// <summary>
    /// The number of the segment within its batch, from 1.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The text of the segment with its tags.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}