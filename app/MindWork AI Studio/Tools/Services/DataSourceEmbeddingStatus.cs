using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <remarks>
/// Counts documents, whatever a document is for the kind of data source: a file for a folder, a
/// mail for a mailbox. Which word the user reads is the business of the page showing it.
///
/// CurrentDocumentBlock and CurrentDocumentPage are null rather than zero while nothing is known
/// about them: a document which is only about to start has no first block, and not every kind of
/// document has pages to count. Block numbers start at one, the way the page states them.
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
    int TotalDocuments,
    int IndexedDocuments,
    int FailedDocuments,
    string CurrentDocument,
    string LastError,
    IReadOnlyList<DataSourceEmbeddingFailure> Failures,
    int PermanentlySkippedDocuments = 0,
    int? CurrentDocumentBlock = null,
    int? CurrentDocumentPage = null,
    bool VectorStoreUnreadable = false)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingStatus).Namespace, nameof(DataSourceEmbeddingStatus));

    /// <remarks>
    /// Documents which were skipped for good are done, even though nothing was indexed of them.
    /// Leaving them out would keep the bar short of the end for a data source which has nothing
    /// left to do.
    /// </remarks>
    public int ProgressPercent => this.TotalDocuments <= 0 ? 0 : Math.Clamp((int)Math.Round((this.IndexedDocuments + this.PermanentlySkippedDocuments) * 100d / this.TotalDocuments), 0, 100);

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
