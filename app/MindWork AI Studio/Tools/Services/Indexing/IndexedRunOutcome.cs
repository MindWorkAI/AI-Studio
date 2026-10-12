namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// How an indexing run ended, as far as the queue is concerned.
/// </summary>
internal enum IndexedRunOutcome
{
    /// <summary>
    /// Nothing is left to do until the data source changes, or somebody asks for a run.
    /// </summary>
    DONE,

    /// <summary>
    /// The run stopped after its share of the work, so other data sources get their turn. The
    /// embedding service queues another run, which carries on where this one stopped.
    /// </summary>
    MORE_TO_DO,
}