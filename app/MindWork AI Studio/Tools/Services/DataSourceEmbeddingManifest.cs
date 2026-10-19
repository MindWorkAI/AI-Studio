namespace AIStudio.Tools.Services;

public sealed class DataSourceEmbeddingManifest
{
    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public Dictionary<string, EmbeddedFileRecord> Files { get; init; } = new(PathTools.COMPARER);

    /// <summary>
    /// The files whose indexing began but never came to an end, by their absolute path.
    /// </summary>
    /// <remarks>
    /// Their row in the index store has no chunks, and some of their vectors may have reached the
    /// collection: the run was canceled while it indexed the file, or the app was closed or crashed
    /// meanwhile. Indexing such a file again replaces both. A file which is gone, or which the data
    /// source no longer selects, is never indexed again, so the indexer has to remove them itself.
    /// </remarks>
    public HashSet<string> UnfinishedFiles { get; init; } = new(PathTools.COMPARER);

    /// <summary>
    /// The files whose indexing failed for a reason which lies in the file itself, keyed by their
    /// absolute path.
    /// </summary>
    /// <remarks>
    /// These files are not read again as long as their fingerprint stays the same. Without this,
    /// a folder holding hundreds of scanned documents without a text layer would be read again on
    /// every single run, with the outcome known in advance.
    /// </remarks>
    public Dictionary<string, PermanentIndexingFailureRecord> PermanentFailures { get; init; } = new(PathTools.COMPARER);
}
