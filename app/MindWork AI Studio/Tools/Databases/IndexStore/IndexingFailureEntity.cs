namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class IndexingFailureEntity
{
    public string ParentFileId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string AbsolutePath { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>
    /// The failure code, stored by name.
    /// </summary>
    /// <remarks>
    /// The enum has no explicit numbers, so storing the name keeps the rows readable across
    /// versions which add or reorder codes.
    /// </remarks>
    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessage { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public EmbeddingStateDataSourceEntity? DataSource { get; set; }
}