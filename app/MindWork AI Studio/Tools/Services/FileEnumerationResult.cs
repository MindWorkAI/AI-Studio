using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed class FileEnumerationResult
{
    private readonly Dictionary<string, int> filePatternExclusionCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> excludedFolderCounts = new(StringComparer.OrdinalIgnoreCase);

    public List<FileInfo> Files { get; } = [];

    public List<DataSourceEmbeddingFailure> Failures { get; } = [];

    public List<DataSourceEmbeddingIgnoredFile> IgnoredFiles { get; } = [];

    public IReadOnlyList<DataSourceEmbeddingFilePatternGroup> FilePatternExclusions => this.filePatternExclusionCounts
        .Select(group => new DataSourceEmbeddingFilePatternGroup(group.Key, group.Value))
        .OrderBy(group => group.FileType, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public int FilePatternExclusionCount => this.filePatternExclusionCounts.Values.Sum();

    public IReadOnlyList<DataSourceEmbeddingExcludedFolderGroup> ExcludedFolders => this.excludedFolderCounts
        .Select(group => new DataSourceEmbeddingExcludedFolderGroup(group.Key, group.Value))
        .OrderBy(group => group.FolderName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public int ExcludedFolderCount => this.excludedFolderCounts.Values.Sum();

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

    public void AddIgnoredFile(string filePath, DataSourceEmbeddingIgnoreReason reason) =>
        this.IgnoredFiles.Add(new DataSourceEmbeddingIgnoredFile(filePath, reason));

    public void AddFilePatternExclusion(string filePath)
    {
        var fileType = GetFilePatternExclusionType(filePath);
        this.filePatternExclusionCounts[fileType] = this.filePatternExclusionCounts.GetValueOrDefault(fileType) + 1;
    }

    public void AddExcludedFolder(string directoryPath)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(directoryPath));
        this.excludedFolderCounts[folderName] = this.excludedFolderCounts.GetValueOrDefault(folderName) + 1;
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
