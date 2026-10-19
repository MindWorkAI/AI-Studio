using System.Diagnostics.CodeAnalysis;

using AIStudio.Provider;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// One way through a data source, from its first step to the step which gets through it.
/// </summary>
/// <remarks>
/// A large data source is indexed in steps, each of them a run of its own, cf. RunShare. What one
/// step learns about documents which failed has to reach the next one, or every step reads them
/// again: that costs time, with a cloud provider money as well, and the list of failures would lose
/// whatever the steps before found. A pass keeps it for as long as the steps go on.
///
/// The embedding service keeps a pass only while the last step of its data source left more to do.
/// Any other end of a run ends the pass, and so does a reset of the stored index. A manual retry
/// begins a new pass, as does a run for other embedding settings or another selection of documents.
///
/// Documents which failed for a reason of their own are none of its business: the index remembers
/// them for good, cf. PermanentIndexingFailure. Only the steps of one data source use a pass, one
/// after the other, so it needs no lock.
/// </remarks>
/// <param name="runSignature">The embedding settings and the selection of documents the pass works for.</param>
internal sealed class IndexingPass(DataSourceRunSignature runSignature)
{
    /// <summary>
    /// How often a document is tried during one pass at most, whether it changed in between or not.
    /// </summary>
    /// <remarks>
    /// A file which changes faster than it can be read would otherwise be read in every step, e.g.
    /// the log file of another program.
    /// </remarks>
    private const int MAX_ATTEMPTS = 2;

    /// <summary>
    /// A document which failed during this pass.
    /// </summary>
    /// <param name="Fingerprint">The fingerprint of the document when it failed the last time.</param>
    /// <param name="Attempts">How often it failed during this pass.</param>
    /// <param name="Failure">Why it failed the last time, as the list of failures shows it.</param>
    private sealed record FailedDocument(string Fingerprint, int Attempts, DataSourceEmbeddingFailure Failure);

    /// <summary>
    /// The documents which failed during this pass for a reason which may pass, by their keys.
    /// </summary>
    private readonly Dictionary<string, FailedDocument> failedDocuments = new(PathTools.COMPARER);

    //
    // Which kinds of provider failure the user was already told about in this pass. A rejected API
    // key is the same problem for every one of a few thousand documents, and one message is what it
    // takes to send the user to the settings. Kept for the whole pass, so a recurring failure such
    // as a rate limit does not show up anew with every step.
    //
    private readonly HashSet<ProviderRequestFailureReason> reportedFailureReasons = [];

    public DataSourceRunSignature RunSignature => runSignature;

    /// <summary>
    /// How many documents failed during this pass and are not tried again for now.
    /// </summary>
    public int FailedDocumentCount => this.failedDocuments.Count;

    /// <summary>
    /// The pass a run works in: the one the last step of its data source left, or a new one.
    /// </summary>
    /// <param name="current">The pass the last step of the data source left, or null when there is none.</param>
    /// <param name="refreshMode">Why the run was asked for.</param>
    /// <param name="runSignature">The embedding settings and the selection of documents the run works for.</param>
    /// <returns>The pass of the run.</returns>
    public static IndexingPass ContinueOrBegin(IndexingPass? current, DataSourceEmbeddingRefreshMode refreshMode, DataSourceRunSignature runSignature)
    {
        //
        // A manual retry is the user asking for every failed document to be tried again, so it
        // never carries on with what a pass knows. Neither does a run for other embedding settings:
        // what failed with the old ones may well work with the new. A run for another selection
        // goes through the data source anew as well, since it works with other documents:
        //
        if (current is not null && refreshMode is not DataSourceEmbeddingRefreshMode.MANUAL_RETRY && current.RunSignature == runSignature)
            return current;

        return new(runSignature);
    }

    /// <summary>
    /// Records that a document failed for a reason which may pass.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="fingerprint">The fingerprint of the document as it was read.</param>
    /// <param name="failure">Why it failed, as the list of failures shows it.</param>
    public void RecordFailure(string documentKey, string fingerprint, DataSourceEmbeddingFailure failure)
    {
        var attempts = this.failedDocuments.TryGetValue(documentKey, out var earlierFailure) ? earlierFailure.Attempts + 1 : 1;
        this.failedDocuments[documentKey] = new(fingerprint, attempts, failure);
    }

    /// <summary>
    /// Forgets that a document failed, e.g. because it was indexed after all.
    /// </summary>
    /// <param name="documentKey">The key of the document.</param>
    public void Forget(string documentKey) => this.failedDocuments.Remove(documentKey);

    /// <summary>
    /// Whether a document failed during this pass and is not tried again before the next one.
    /// </summary>
    /// <remarks>
    /// A document which changed since it failed gets another try, as long as it did not fail
    /// MAX_ATTEMPTS times during this pass already.
    /// </remarks>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="fingerprint">The fingerprint of the document as it is now.</param>
    /// <param name="failure">Why it failed the last time, when it is not tried again.</param>
    /// <returns>True when the document is left alone.</returns>
    public bool TryGetEarlierFailure(string documentKey, string fingerprint, [NotNullWhen(true)] out DataSourceEmbeddingFailure? failure)
    {
        failure = null;
        if (!this.failedDocuments.TryGetValue(documentKey, out var failedDocument))
            return false;

        if (failedDocument.Attempts < MAX_ATTEMPTS && !string.Equals(failedDocument.Fingerprint, fingerprint, StringComparison.Ordinal))
            return false;

        failure = failedDocument.Failure;
        return true;
    }

    /// <summary>
    /// Marks a kind of provider failure as reported to the user.
    /// </summary>
    /// <param name="reason">The kind of provider failure.</param>
    /// <returns>True when the user was not told about it during this pass yet.</returns>
    public bool TryMarkReported(ProviderRequestFailureReason reason) => this.reportedFailureReasons.Add(reason);
}