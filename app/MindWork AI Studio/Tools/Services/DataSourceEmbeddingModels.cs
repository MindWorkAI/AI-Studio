using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingOverview(
    bool IsVisible,
    DataSourceEmbeddingState State,
    int IndexedFiles,
    int TotalFiles,
    int FailedFiles);

public enum DataSourceEmbeddingState
{
    IDLE,
    QUEUED,
    RUNNING,
    COMPLETED,
    FAILED,
}

public sealed record DataSourceEmbeddingStatus(
    string DataSourceId,
    string DataSourceName,
    DataSourceType DataSourceType,
    DataSourceEmbeddingState State,
    int TotalFiles,
    int IndexedFiles,
    int FailedFiles,
    string CurrentFile,
    string LastError)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingService).Namespace, nameof(DataSourceEmbeddingService));

    public int ProgressPercent => this.TotalFiles <= 0 ? 0 : Math.Clamp((int)Math.Round(this.IndexedFiles * 100d / this.TotalFiles), 0, 100);

    public string StateLabel => this.State switch
    {
        DataSourceEmbeddingState.QUEUED => TB("Queued"),
        DataSourceEmbeddingState.RUNNING => TB("Running"),
        DataSourceEmbeddingState.COMPLETED => TB("Completed"),
        DataSourceEmbeddingState.FAILED => TB("Needs attention"),
        _ => TB("Idle")
    };

    public int SortOrder => this.State switch
    {
        DataSourceEmbeddingState.RUNNING => 0,
        DataSourceEmbeddingState.QUEUED => 1,
        DataSourceEmbeddingState.FAILED => 2,
        DataSourceEmbeddingState.COMPLETED => 3,
        _ => 4,
    };
}

public sealed class FileEnumerationResult
{
    public List<FileInfo> Files { get; } = [];

    public int FailedFiles { get; set; }

    public string LastError { get; set; } = string.Empty;
}

public sealed class PersistedEmbeddingState
{
    public Dictionary<string, DataSourceEmbeddingManifest> DataSources { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DataSourceEmbeddingManifest
{
    public string EmbeddingProviderId { get; set; } = string.Empty;

    public string EmbeddingSignature { get; set; } = string.Empty;

    public int VectorSize { get; set; }

    public Dictionary<string, EmbeddedFileRecord> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record EmbeddedFileRecord(
    string Fingerprint,
    long FileSize,
    DateTime LastWriteUtc,
    DateTime EmbeddedAtUtc,
    int ChunkCount);
