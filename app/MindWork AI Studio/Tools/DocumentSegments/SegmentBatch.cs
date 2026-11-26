namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The segments a model works on in one request, and the form of its answer.
/// </summary>
/// <remarks>
/// The request and the answer have the same form, so a model only has to replace the texts. Neither names
/// what the model does with them: the same form serves a translation as well as a correction.
/// </remarks>
public sealed class SegmentBatch
{
    /// <summary>
    /// The segments, each with its number.
    /// </summary>
    public List<SegmentItem> Segments { get; set; } = [];
}