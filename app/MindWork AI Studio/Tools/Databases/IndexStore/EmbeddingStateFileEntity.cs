namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class EmbeddingStateFileEntity
{
    public string ParentFileId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string AbsolutePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTimeOffset CreationUtc { get; set; }

    public DateTimeOffset LastWriteUtc { get; set; }

    public DateTimeOffset EmbeddedAtUtc { get; set; }

    public int ChunkCount { get; set; }

    public string? TranscriptKey { get; set; }

    /// <summary>
    /// The SHA-256 hash of the raw bytes of the file when it was indexed, cf. FileContentHash, or
    /// null for a file indexed before v26.10.1 and for every mail.
    /// </summary>
    /// <remarks>
    /// The fingerprint says only that path, size or write time changed. When it does, the content
    /// hash tells whether the content changed as well, or whether the file was merely saved again,
    /// synchronized or copied, which needs neither extraction nor embedding.
    /// </remarks>
    public string? ContentHash { get; set; }

    public EmbeddingStateDataSourceEntity? DataSource { get; set; }

    public List<EmbeddingStateChunkEntity> Chunks { get; set; } = [];
}
