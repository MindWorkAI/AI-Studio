namespace AIStudio.Tools.Databases.IndexStore;

public sealed record IndexStoreSearchResult(
    string ChunkId,
    string ParentFileId,
    string DataSourceId,
    string DataSourceType,
    string AbsolutePath,
    string FileName,
    string RelativePath,
    string FileType,
    int? PageNumber,
    int? EndPageNumber,
    int ChunkIndex,
    string ChunkText,
    double Score,
    string Fingerprint,
    long FileSize,
    DateTimeOffset CreationUtc,
    DateTimeOffset LastWriteUtc,
    DateTimeOffset EmbeddedAtUtc,
    int ChunkCount,
    string? SheetName = null,
    int? StartRow = null,
    int? EndRow = null,
    int? StartLine = null,
    int? EndLine = null,
    int? AttachmentPosition = null)
{
    /// <summary>
    /// Where the chunk is in its document.
    /// </summary>
    public ChunkLocation Location => new(this.PageNumber, this.EndPageNumber, this.SheetName, this.StartRow, this.EndRow, this.StartLine, this.EndLine, this.AttachmentPosition);
}
