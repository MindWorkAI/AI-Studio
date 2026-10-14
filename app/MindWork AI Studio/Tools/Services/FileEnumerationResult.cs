using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class FileEnumerationResult
{
    private readonly Dictionary<string, int> filePatternExclusionCounts = new(StringComparer.OrdinalIgnoreCase);

    public List<FileInfo> Files { get; } = [];

    public List<DataSourceEmbeddingFailure> Failures { get; } = [];

    public List<DataSourceEmbeddingIgnoredFile> IgnoredFiles { get; } = [];

    public IReadOnlyList<DataSourceEmbeddingFilePatternGroup> FilePatternExclusions => this.filePatternExclusionCounts
        .Select(group => new DataSourceEmbeddingFilePatternGroup(group.Key, group.Value))
        .OrderBy(group => group.FileType, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public int FilePatternExclusionCount => this.filePatternExclusionCounts.Values.Sum();

    public int FailedFiles { get; set; }

    public string LastError { get; set; } = string.Empty;

    public void AddFailure(string filePath, string reason)
    {
        this.Failures.Add(new DataSourceEmbeddingFailure(filePath, reason, DateTimeOffset.UtcNow));
        this.FailedFiles = this.Failures.Count;
        this.LastError = reason;
    }

    public void AddIgnoredFile(string filePath, DataSourceEmbeddingIgnoreReason reason) =>
        this.IgnoredFiles.Add(new DataSourceEmbeddingIgnoredFile(filePath, reason));

    public void AddFilePatternExclusion(string filePath)
    {
        var fileType = GetFilePatternExclusionType(filePath);
        this.filePatternExclusionCounts[fileType] = this.filePatternExclusionCounts.GetValueOrDefault(fileType) + 1;
    }

    private static string GetFilePatternExclusionType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(extension))
            return extension;

        var fileName = Path.GetFileName(filePath);
        return FileTypes.SOURCE_LIKE_FILE_NAMES.FilterExtensions
            .FirstOrDefault(knownFileName => string.Equals(knownFileName, fileName, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }
}
