namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One way to cut a text into smaller units, tried when the text is too large for a single chunk.
/// </summary>
/// <param name="Name">The name of the rule, for the log.</param>
/// <param name="Split">Cuts a text into units, given the text and the segments it was made of. Null means a hard cut.</param>
/// <param name="UsesSourceSegmentCounts">Whether the units are the segments themselves, so their token counts and pages can be reused.</param>
internal sealed record ChunkingRule(string Name, Func<string, IReadOnlyList<string>, IReadOnlyList<string>>? Split, bool UsesSourceSegmentCounts = false);