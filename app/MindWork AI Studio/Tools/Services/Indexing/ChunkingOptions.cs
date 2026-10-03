namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How large the chunks of a data source may become, and how much of each one the next repeats.
/// </summary>
/// <param name="MaxChunkTokenLength">The largest chunk in tokens of the embedding provider.</param>
/// <param name="OverlapTokenLength">How many tokens of a chunk the next one starts with.</param>
internal sealed record ChunkingOptions(int MaxChunkTokenLength, int OverlapTokenLength);