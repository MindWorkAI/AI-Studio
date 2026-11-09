namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Groups the segments of a document into batches, each of which a model works on in one request.
/// </summary>
/// <remarks>
/// AI Studio knows how much a model reads, but not how much it may write in one answer, and a translation
/// is about as long as its source. The batches therefore stay small enough for the output limits models
/// commonly have, e.g., the 4,096 tokens Anthropic answers with by default.
/// </remarks>
public static class SegmentBatchPlanner
{
    /// <summary>
    /// How many characters of text with tags one batch holds at most, unless a single segment is longer.
    /// </summary>
    public const int MAX_BATCH_CHARACTERS = 3_000;

    /// <summary>
    /// How many segments one batch holds at most, so that a model does not lose track of their numbers.
    /// </summary>
    public const int MAX_BATCH_SEGMENTS = 40;

    /// <summary>
    /// Groups the segments into batches.
    /// </summary>
    /// <remarks>
    /// A segment whose text appeared before, e.g., a heading repeated in every footer, goes to the model only
    /// once. Segments without letters stay out, since they stay as they are. A segment longer than a batch
    /// gets a batch of its own.
    /// </remarks>
    /// <param name="segments">The segments, in the order of the document.</param>
    /// <param name="maxCharacters">How many characters one batch holds at most.</param>
    /// <param name="maxSegments">How many segments one batch holds at most.</param>
    /// <returns>The batches, in the order of the document.</returns>
    public static IReadOnlyList<IReadOnlyList<DocumentSegment>> Plan(IEnumerable<DocumentSegment> segments, int maxCharacters = MAX_BATCH_CHARACTERS, int maxSegments = MAX_BATCH_SEGMENTS)
    {
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var batches = new List<IReadOnlyList<DocumentSegment>>();
        var batch = new List<DocumentSegment>();
        var batchCharacters = 0;
        foreach (var segment in segments)
        {
            if (!segment.HasWords || !seenKeys.Add(segment.Key))
                continue;

            var characters = segment.Key.Length;
            if (batch.Count > 0 && (batch.Count >= maxSegments || batchCharacters + characters > maxCharacters))
            {
                batches.Add(batch);
                batch = [];
                batchCharacters = 0;
            }

            batch.Add(segment);
            batchCharacters += characters;
        }

        if (batch.Count > 0)
            batches.Add(batch);

        return batches;
    }
}