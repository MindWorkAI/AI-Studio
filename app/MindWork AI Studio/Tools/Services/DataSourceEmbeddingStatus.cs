using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

public sealed record DataSourceEmbeddingStatus(
    string DataSourceId,
    string DataSourceName,
    DataSourceType DataSourceType,
    DataSourceEmbeddingState State,
    int TotalFiles,
    int IndexedFiles,
    int FailedFiles,
    string CurrentFile,
    string LastError,
    IReadOnlyList<DataSourceEmbeddingFailure> Failures)
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
