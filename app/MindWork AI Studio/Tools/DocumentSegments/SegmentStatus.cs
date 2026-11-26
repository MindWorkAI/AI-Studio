namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// What became of a segment.
/// </summary>
public enum SegmentStatus
{
    /// <summary>
    /// A model worked on the segment, e.g., translated it.
    /// </summary>
    TRANSFORMED,

    /// <summary>
    /// The prompt injection filter changed the segment, so it never reached a model and stays as it is.
    /// </summary>
    FILTERED,

    /// <summary>
    /// No model delivered a result for the segment, so it stays as it is.
    /// </summary>
    FAILED,
}