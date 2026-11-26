namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Whether a format wrote the new document.
/// </summary>
public enum SegmentWriteOutcome
{
    /// <summary>
    /// The new document is written.
    /// </summary>
    WRITTEN,

    /// <summary>
    /// The source holds segments the results do not know, i.e., it changed since it was read, so nothing is written.
    /// </summary>
    SOURCE_CHANGED,
}