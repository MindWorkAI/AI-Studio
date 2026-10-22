namespace AIStudio.Components;

/// <summary>
/// Why a data source is listed in the selection, but cannot be picked.
/// </summary>
/// <remarks>
/// A reason rather than a yes or no, because the row has to say something different for each of
/// them: one asks the user to wait, the other one asks them to act. Asking somebody to wait for
/// something which will never happen on its own is the worse of the two mistakes.
/// </remarks>
public enum DataSourceBlockReason
{
    /// <summary>
    /// Nothing is in the way, the data source can be picked.
    /// </summary>
    NONE,

    /// <summary>
    /// The index has to be built anew before this data source can answer a search. This passes by
    /// itself, as soon as the background indexing has worked through the data source.
    /// </summary>
    AWAITING_REINDEX,

    /// <summary>
    /// The index cannot be read anymore. This does not pass by itself: only the user can start the
    /// rebuild, because it sends every document to the embedding provider once more.
    /// </summary>
    NEEDS_REPAIR,
}