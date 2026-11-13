namespace AIStudio.Tools.Databases.IndexStore;

/// <remarks>
/// The mail tools search, count and read mails through these. Each one asks about one mailbox,
/// so a mail of another mailbox never turns up, whatever id a caller hands in.
/// </remarks>
public abstract partial class IndexStoreClient
{
    /// <summary>
    /// Lists the mails of a mailbox which meet the conditions, the most recently received first.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="offset">How many mails to skip, for the pages after the first one.</param>
    /// <param name="limit">How many mails to list at most.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The ids of the mails. The order stays the same between calls, so pages neither repeat nor skip a mail.</returns>
    public abstract Task<IReadOnlyList<string>> QueryMailsAsync(string dataSourceId, MailFilter filter, int offset, int limit, CancellationToken token);

    /// <summary>
    /// Reads the ids of every chunk of the mails which meet the conditions, to restrict a vector search to them.
    /// </summary>
    /// <remarks>
    /// The list can be long: it holds every chunk of every matching mail, and a broad condition
    /// matches most of a mailbox. A caller without any condition does better to search the whole
    /// store and drop what GetMailSummariesAsync does not know.
    /// </remarks>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunk ids, which are the ids of their points in the vector store.</returns>
    public abstract Task<IReadOnlyList<string>> GetMailChunkIdsAsync(string dataSourceId, MailFilter filter, CancellationToken token);

    /// <summary>
    /// Searches the chunks of the mails which meet the conditions by their words, with BM25.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="query">The words to search for.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="maxMatches">How many chunks to return at most.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chunks, best first; the id of their document is the id of their mail.</returns>
    public abstract Task<IReadOnlyList<IndexStoreSearchResult>> SearchMailChunksAsync(string dataSourceId, string query, MailFilter filter, int maxMatches, CancellationToken token);

    /// <summary>
    /// Counts the mails of a mailbox which meet the conditions.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="grouping">How to break the number down.</param>
    /// <param name="maxGroups">How many groups to return at most, the largest ones.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The total and the largest groups.</returns>
    public abstract Task<MailCountResult> CountMailsAsync(string dataSourceId, MailFilter filter, MailCountGrouping grouping, int maxGroups, CancellationToken token);

    /// <summary>
    /// Reads what a list of mails shows about each of the given ones.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="mailIds">The ids of the mails, in the order the list shows them.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The summaries in the given order. A mail this mailbox does not hold, or which lies nowhere any more, is left out.</returns>
    public abstract Task<IReadOnlyList<MailSummary>> GetMailSummariesAsync(string dataSourceId, IReadOnlyList<string> mailIds, CancellationToken token);

    /// <summary>
    /// Finds a mail by its Message-ID, e.g., the one another mail replies to.
    /// </summary>
    /// <param name="dataSourceId">The mailbox.</param>
    /// <param name="messageId">The Message-ID, without angle brackets.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The id of the mail, the one received first when there are copies; null when the mailbox holds no such mail.</returns>
    public abstract Task<string?> FindMailByMessageIdAsync(string dataSourceId, string messageId, CancellationToken token);
}