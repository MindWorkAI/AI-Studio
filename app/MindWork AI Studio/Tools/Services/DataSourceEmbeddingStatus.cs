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
/// difference to offer the repair for this one case: the vector store cannot be read, or it is
/// missing although the index database knows what it held. It is carried as its own flag so nothing
/// has to read it back out of the message in LastError. Attention does the same for what the user
/// has to decide, and PendingRemovalCount is the number a held back removal asks about: agreeing to
/// it hands that very number back, cf. ApprovePendingMailRemovalAsync.
///
/// LastSyncUtc is when the data source was last worked through as a whole, for those which are
/// synced rather than watched, such as mailboxes. It stays null for the others.
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
    bool VectorStoreUnreadable = false,
    DataSourceAttention Attention = DataSourceAttention.NONE,
    int? PendingRemovalCount = null,
    DateTimeOffset? LastSyncUtc = null)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(DataSourceEmbeddingStatus).Namespace, nameof(DataSourceEmbeddingStatus));

    /// <summary>
    /// Files which were found in the scanned part of a local data source but intentionally not
    /// indexed.
    /// </summary>
    public IReadOnlyList<DataSourceEmbeddingIgnoredFile> IgnoredFiles { get; init; } = [];

    /// <summary>
    /// Files outside the configured file patterns or named by the exclusion list, summarized by file
    /// type. They are intentional exclusions rather than skipped documents and therefore do not
    /// affect progress or status.
    /// </summary>
    public IReadOnlyList<DataSourceEmbeddingFilePatternGroup> FilePatternExclusions { get; init; } = [];

    /// <summary>
    /// Folders which the exclusion list left out, summarized by name. Like the files outside the
    /// patterns, they are intentional and do not affect progress or status.
    /// </summary>
    public IReadOnlyList<DataSourceEmbeddingExcludedFolderGroup> ExcludedFolders { get; init; } = [];

    /// <summary>
    /// Every selected document the run skipped, whether it may become readable after changing or
    /// was excluded before extraction. Files outside the configured patterns are not selected and
    /// therefore do not count here.
    /// </summary>
    public int SkippedDocuments => this.PermanentlySkippedDocuments + this.IgnoredFiles.Count;

    /// <summary>
    /// The settled health of this data source. Activity is represented by State
    /// instead, so queued and running sources can keep their activity indicator.
    /// </summary>
    /// <remarks>
    /// A few permanently unreadable files do not make an otherwise usable data source unhealthy.
    /// They become a warning only when none of the selected documents could be indexed. Files
    /// outside the configured patterns never take part in this decision.
    /// </remarks>
    public DataSourceEmbeddingHealth Health
    {
        get
        {
            if (this.State is DataSourceEmbeddingState.FAILED || this.FailedDocuments > 0)
                return DataSourceEmbeddingHealth.ERROR;

            if (this.State is DataSourceEmbeddingState.COMPLETED && this.IndexedDocuments == 0 && this.SkippedDocuments > 0)
                return DataSourceEmbeddingHealth.WARNING;

            return DataSourceEmbeddingHealth.HEALTHY;
        }
    }

    /// <remarks>
    /// Skipped documents are done for this run, even though nothing was indexed of them. Leaving
    /// them out would keep the bar short of the end for a data source which has nothing left to do.
    /// </remarks>
    public int ProgressPercent => this.TotalDocuments <= 0 ? 0 : Math.Clamp((int)Math.Round((this.IndexedDocuments + this.SkippedDocuments) * 100d / this.TotalDocuments), 0, 100);

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
