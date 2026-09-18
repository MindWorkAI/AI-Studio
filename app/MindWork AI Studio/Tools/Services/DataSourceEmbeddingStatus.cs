using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <remarks>
/// CurrentFileBlock and CurrentFilePage are null rather than zero while nothing is known about
/// them: a file which is only about to start has no first block, and not every kind of document
/// has pages to count. Block numbers start at one, the way the page states them.
///
/// VectorStoreUnreadable says why a data source failed, not only that it did. The UI needs that
/// difference to offer the repair for this one case, and it is carried as its own flag so nothing
/// has to read it back out of the message in LastError.
/// </remarks>
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
    IReadOnlyList<DataSourceEmbeddingFailure> Failures,
    int PermanentlySkippedFiles = 0,
    int? CurrentFileBlock = null,
    int? CurrentFilePage = null,
    bool VectorStoreUnreadable = false)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingStatus).Namespace, nameof(DataSourceEmbeddingStatus));

    /// <remarks>
    /// Files which were skipped for good are done, even though nothing was indexed of them.
    /// Leaving them out would keep the bar short of the end for a data source which has nothing
    /// left to do.
    /// </remarks>
    public int ProgressPercent => this.TotalFiles <= 0 ? 0 : Math.Clamp((int)Math.Round((this.IndexedFiles + this.PermanentlySkippedFiles) * 100d / this.TotalFiles), 0, 100);

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
