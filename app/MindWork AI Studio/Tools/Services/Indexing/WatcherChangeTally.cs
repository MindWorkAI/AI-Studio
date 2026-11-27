namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Counts the file system changes which the watchers report, so the log gets a summary instead of one line per change.
/// </summary>
/// <remarks>
/// A build, a checkout, or a sync tool changes thousands of files within seconds. With one log line each, such a
/// burst buried the log, and the log events piled up on their way to the runtime until it closed the connection
/// of the .NET server. Therefore, the first change of a data source opens a window and is logged as it comes;
/// every further change is only counted until the window closes, and its summary takes one line.
/// </remarks>
internal sealed class WatcherChangeTally
{
    private readonly Lock tallyLock = new();
    private readonly Dictionary<string, WatcherChangeCounts> openWindows = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Counts a change of a data source.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <param name="path">The path of the change.</param>
    /// <param name="isRelevant">Whether the change concerns the RAG index.</param>
    /// <returns>True when the change opened a window: the caller logs it as it comes and closes the window later.</returns>
    public bool Count(string dataSourceId, string path, bool isRelevant)
    {
        lock (this.tallyLock)
        {
            if (this.openWindows.TryGetValue(dataSourceId, out var counts))
            {
                this.openWindows[dataSourceId] = counts.Add(path, isRelevant);
                return false;
            }

            this.openWindows[dataSourceId] = WatcherChangeCounts.NONE;
            return true;
        }
    }

    /// <summary>
    /// Closes the window of a data source, so its next change opens a new one.
    /// </summary>
    /// <param name="dataSourceId">The id of the data source.</param>
    /// <returns>The changes counted after the one which opened the window.</returns>
    public WatcherChangeCounts Close(string dataSourceId)
    {
        lock (this.tallyLock)
            return this.openWindows.Remove(dataSourceId, out var counts) ? counts : WatcherChangeCounts.NONE;
    }
}