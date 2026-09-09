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

    public string ConfidenceLevel { get; set; } = string.Empty;

    public int ConfidenceLevelRank { get; set; }

    public EmbeddingStateDataSourceEntity? DataSource { get; set; }

    public List<EmbeddingStateChunkEntity> Chunks { get; set; } = [];
}
