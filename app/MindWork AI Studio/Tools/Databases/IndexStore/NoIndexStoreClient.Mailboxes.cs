namespace AIStudio.Tools.Databases.IndexStore;

public sealed partial class NoIndexStoreClient
{
    public override Task<IReadOnlyList<MailFolderRecord>> GetMailFoldersAsync(string dataSourceId, CancellationToken token) => Task.FromResult<IReadOnlyList<MailFolderRecord>>([]);

    public override Task UpsertMailFolderAsync(string dataSourceId, MailFolderRecord folder, CancellationToken token) => Task.CompletedTask;

    public override Task DeleteMailFolderAsync(string dataSourceId, string folderPath, CancellationToken token) => Task.CompletedTask;

    public override Task UpsertMailAsync(string dataSourceId, MailRecord mail, CancellationToken token) => Task.CompletedTask;

    public override Task<MailRecord?> GetMailAsync(string dataSourceId, string mailId, CancellationToken token) => Task.FromResult<MailRecord?>(null);

    public override Task<bool> AddMailLocationAsync(string dataSourceId, string mailId, MailLocationRecord location, CancellationToken token) => Task.FromResult(false);

    public override Task RemoveMailLocationsAsync(string dataSourceId, string folderPath, IReadOnlyCollection<long> uids, CancellationToken token) => Task.CompletedTask;

    public override Task<IReadOnlyDictionary<long, MailFlags>> GetMailLocationsAsync(string dataSourceId, string folderPath, CancellationToken token) =>
        Task.FromResult<IReadOnlyDictionary<long, MailFlags>>(new Dictionary<long, MailFlags>());

    public override Task UpdateMailFlagsAsync(string dataSourceId, string folderPath, IReadOnlyDictionary<long, MailFlags> flagsByUid, CancellationToken token) => Task.CompletedTask;

    public override Task<IReadOnlyList<string>> GetOrphanedMailsAsync(string dataSourceId, DateTimeOffset orphanedBefore, CancellationToken token) => Task.FromResult<IReadOnlyList<string>>([]);

    public override Task<MailboxSyncState> GetMailboxSyncStateAsync(string dataSourceId, CancellationToken token) => Task.FromResult(new MailboxSyncState(null, null, null));

    public override Task HoldBackMailRemovalAsync(string dataSourceId, int removalCount, CancellationToken token) => Task.CompletedTask;

    public override Task<bool> ApprovePendingMailRemovalAsync(string dataSourceId, int removalCount, CancellationToken token) => Task.FromResult(false);

    public override Task CompleteMailboxSyncAsync(string dataSourceId, DateTimeOffset completedUtc, CancellationToken token) => Task.CompletedTask;

    public override Task<MailboxAuthFailure?> GetMailboxAuthFailureAsync(string dataSourceId, CancellationToken token) => Task.FromResult<MailboxAuthFailure?>(null);

    public override Task UpsertMailboxAuthFailureAsync(string dataSourceId, MailboxAuthFailure failure, CancellationToken token) => Task.CompletedTask;

    public override Task ClearMailboxAuthFailureAsync(string dataSourceId, CancellationToken token) => Task.CompletedTask;
}