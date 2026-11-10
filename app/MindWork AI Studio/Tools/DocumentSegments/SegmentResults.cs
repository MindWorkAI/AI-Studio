using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// What became of the segments of a document, found by the text of each segment.
/// </summary>
/// <remarks>
/// The results stay unchanged once created, so that an assistant can keep them while the user saves the
/// document, maybe more than once. A segment without letters needs no result: it stays as it is anyway.
/// </remarks>
public sealed class SegmentResults
{
    private readonly FrozenDictionary<string, SegmentOutcome> outcomes;

    /// <summary>
    /// Creates the results.
    /// </summary>
    /// <param name="outcomes">What became of each segment, by its Key.</param>
    public SegmentResults(IReadOnlyDictionary<string, SegmentOutcome> outcomes) => this.outcomes = outcomes.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// How many distinct segments ended in the given way.
    /// </summary>
    /// <param name="status">What became of the segments.</param>
    public int CountOf(SegmentStatus status) => this.outcomes.Values.Count(outcome => outcome.Status == status);

    /// <summary>
    /// Whether these results know what became of a segment.
    /// </summary>
    /// <remarks>
    /// A format checks this for every segment before it writes anything: a segment with letters which the
    /// results do not know means that the document changed since it was read.
    /// </remarks>
    /// <param name="segment">A segment of the document.</param>
    public bool Covers(DocumentSegment segment) => !segment.HasWords || this.outcomes.ContainsKey(segment.Key);

    /// <summary>
    /// Gets the new text of a segment, when a model worked on it.
    /// </summary>
    /// <param name="segment">A segment of the document.</param>
    /// <param name="text">The new text with tags, or null when the segment stays as it is.</param>
    /// <returns>Whether the segment has a new text.</returns>
    public bool TryGetText(DocumentSegment segment, [NotNullWhen(true)] out string? text)
    {
        if (this.outcomes.TryGetValue(segment.Key, out var outcome) && outcome is { Status: SegmentStatus.TRANSFORMED, Text: { } newText })
        {
            text = newText;
            return true;
        }

        text = null;
        return false;
    }
}