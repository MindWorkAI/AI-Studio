namespace AIStudio.Tools.Databases.IndexStore;

public sealed partial class NoIndexStoreClient
{
    public override Task<IReadOnlyList<string>> QueryMailsAsync(string dataSourceId, MailFilter filter, int offset, int limit, CancellationToken token) => Task.FromResult<IReadOnlyList<string>>([]);

    public override Task<IReadOnlyList<string>> GetMailChunkIdsAsync(string dataSourceId, MailFilter filter, CancellationToken token) => Task.FromResult<IReadOnlyList<string>>([]);

    public override Task<IReadOnlyList<IndexStoreSearchResult>> SearchMailChunksAsync(string dataSourceId, string query, MailFilter filter, int maxMatches, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<IndexStoreSearchResult>>([]);

    public override Task<MailCountResult> CountMailsAsync(string dataSourceId, MailFilter filter, MailCountGrouping grouping, int maxGroups, CancellationToken token) => Task.FromResult(new MailCountResult(0, []));

    public override Task<IReadOnlyList<MailSummary>> GetMailSummariesAsync(string dataSourceId, IReadOnlyList<string> mailIds, CancellationToken token) => Task.FromResult<IReadOnlyList<MailSummary>>([]);

    public override Task<string?> FindMailByMessageIdAsync(string dataSourceId, string messageId, CancellationToken token) => Task.FromResult<string?>(null);
}