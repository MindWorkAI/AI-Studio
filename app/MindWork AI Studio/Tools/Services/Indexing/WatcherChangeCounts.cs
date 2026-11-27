namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The file system changes of one data source which WatcherChangeTally counted instead of logging each.
/// </summary>
/// <param name="Ignored">How many changes concerned no file of the RAG index.</param>
/// <param name="Relevant">How many changes concerned the RAG index.</param>
/// <param name="LastPath">The path of the last change, or an empty string without any.</param>
internal readonly record struct WatcherChangeCounts(int Ignored, int Relevant, string LastPath)
{
    /// <summary>
    /// No change counted.
    /// </summary>
    public static readonly WatcherChangeCounts NONE = new(0, 0, string.Empty);

    /// <summary>
    /// How many changes were counted.
    /// </summary>
    public int Total => this.Ignored + this.Relevant;

    /// <summary>
    /// Counts one more change.
    /// </summary>
    /// <param name="path">The path of the change.</param>
    /// <param name="isRelevant">Whether the change concerns the RAG index.</param>
    /// <returns>The counts including this change.</returns>
    public WatcherChangeCounts Add(string path, bool isRelevant) => isRelevant
        ? this with { Relevant = this.Relevant + 1, LastPath = path }
        : this with { Ignored = this.Ignored + 1, LastPath = path };
}