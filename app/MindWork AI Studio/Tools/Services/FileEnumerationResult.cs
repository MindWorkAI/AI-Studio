namespace AIStudio.Tools.Services;

public sealed class FileEnumerationResult
{
    public List<FileInfo> Files { get; } = [];

    public List<DataSourceEmbeddingFailure> Failures { get; } = [];

    public int FailedFiles { get; set; }

    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// The folders whose content could not be listed. The files below them are out of reach, not gone.
    /// </summary>
    public List<string> UnreadableDirectories { get; } = [];

    /// <summary>
    /// Why the file or the folder of the data source itself could not be reached, or null when it could.
    /// </summary>
    public string? UnreachableRootReason { get; set; }

    public void AddFailure(string filePath, string reason)
    {
        this.Failures.Add(new DataSourceEmbeddingFailure(filePath, reason, DateTimeOffset.UtcNow));
        this.FailedFiles = this.Failures.Count;
        this.LastError = reason;
    }

    public void AddUnreadableDirectory(string directoryPath, string reason)
    {
        this.UnreadableDirectories.Add(directoryPath);
        this.AddFailure(directoryPath, reason);
    }
}