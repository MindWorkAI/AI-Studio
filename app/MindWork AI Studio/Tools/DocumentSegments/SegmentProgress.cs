namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// How far a model got with the segments of a document.
/// </summary>
/// <param name="DoneSegments">How many distinct segments are done, whatever became of them.</param>
/// <param name="TotalSegments">How many distinct segments a model works on in total.</param>
public readonly record struct SegmentProgress(int DoneSegments, int TotalSegments);