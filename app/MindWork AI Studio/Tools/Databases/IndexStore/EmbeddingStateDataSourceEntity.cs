namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class EmbeddingStateDataSourceEntity
{
    public string DataSourceId { get; set; } = string.Empty;

    public string DataSourceName { get; set; } = string.Empty;

    public string DataSourceType { get; set; } = string.Empty;

    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public List<EmbeddingStateFileEntity> Files { get; set; } = [];
}
