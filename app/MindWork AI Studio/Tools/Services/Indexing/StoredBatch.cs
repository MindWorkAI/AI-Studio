namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How many chunks of a batch were stored, cf. IndexedRunContext.FlushBatchAsync.
/// </summary>
/// <param name="StoredCount">How many of the chunks were stored.</param>
/// <param name="ReusedCount">How many of those kept a stored vector instead of being embedded.</param>
internal readonly record struct StoredBatch(int StoredCount, int ReusedCount);