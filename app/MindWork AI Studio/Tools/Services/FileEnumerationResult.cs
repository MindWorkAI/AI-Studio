namespace AIStudio.Tools.Services;

public sealed class FileEnumerationResult
{
    public List<FileInfo> Files { get; } = [];

    public List<DataSourceEmbeddingFailure> Failures { get; } = [];

    public int FailedFiles { get; set; }

    public string LastError { get; set; } = string.Empty;

    public void AddFailure(string filePath, string reason)
    {
        this.Failures.Add(new DataSourceEmbeddingFailure(filePath, reason));
        this.FailedFiles = this.Failures.Count;
        this.LastError = reason;
    }
}
