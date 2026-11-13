namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The rules a kind of text is cut by, from the coarsest to the finest.
/// </summary>
/// <param name="Name">The name of the strategy, for the log.</param>
/// <param name="Rules">The rules, tried one after the other until every chunk fits.</param>
internal sealed record ChunkingStrategy(string Name, IReadOnlyList<ChunkingRule> Rules);