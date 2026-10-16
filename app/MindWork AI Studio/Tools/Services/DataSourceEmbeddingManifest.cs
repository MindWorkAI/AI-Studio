namespace AIStudio.Tools.Services;

public sealed class DataSourceEmbeddingManifest
{
    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public Dictionary<string, EmbeddedFileRecord> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The files whose indexing failed for a reason which lies in the file itself, keyed by their
    /// absolute path.
    /// </summary>
    /// <remarks>
    /// These files are not read again as long as their fingerprint stays the same. Without this,
    /// a folder holding hundreds of scanned documents without a text layer would be read again on
    /// every single run, with the outcome known in advance.
    /// </remarks>
    public Dictionary<string, PermanentIndexingFailureRecord> PermanentFailures { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
