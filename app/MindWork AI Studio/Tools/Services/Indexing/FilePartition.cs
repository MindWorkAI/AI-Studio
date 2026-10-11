namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The files of a data source, sorted by what one run of the file indexer does with them.
/// </summary>
/// <remarks>
/// Only the last group costs time. The others are counted before the first file is read, so the
/// progress of a step starts where the step before left off, rather than at zero.
/// </remarks>
/// <param name="UnchangedCount">How many files were indexed and have not changed since.</param>
/// <param name="StillUnreadable">The files which failed for a reason of their own and have not changed since, each with the stored reason.</param>
/// <param name="FailedEarlierInPass">Why each file failed which failed during an earlier step of the pass, and is not tried again before the next pass.</param>
/// <param name="ToRead">The files the run reads, in the order it reads them.</param>
internal sealed record FilePartition(int UnchangedCount, IReadOnlyList<KeyValuePair<string, PermanentIndexingFailureRecord>> StillUnreadable, IReadOnlyList<DataSourceEmbeddingFailure> FailedEarlierInPass, IReadOnlyList<FileInfo> ToRead);