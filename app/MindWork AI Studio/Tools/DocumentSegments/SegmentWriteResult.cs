namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// How a format wrote the new document.
/// </summary>
/// <param name="Outcome">Whether the new document is written.</param>
/// <param name="ExactCount">How many segments got their new text with all of their inline structure.</param>
/// <param name="SimplifiedCount">How many segments got their new text without parts of their inline structure, because the tags of the model did not fit.</param>
public readonly record struct SegmentWriteResult(SegmentWriteOutcome Outcome, int ExactCount, int SimplifiedCount)
{
    /// <summary>
    /// The result when the source changed since it was read, and nothing is written.
    /// </summary>
    public static readonly SegmentWriteResult SOURCE_CHANGED = new(SegmentWriteOutcome.SOURCE_CHANGED, 0, 0);
}