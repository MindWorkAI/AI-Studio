namespace AIStudio.Tools.Services;

public sealed class DataSourceEmbeddingManifest
{
    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public Dictionary<string, EmbeddedFileRecord> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
