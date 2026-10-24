using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// Stands in for the provenance store while it cannot serve: it keeps nothing and finds nothing.
/// </summary>
/// <remarks>
/// Storing answers with no id, which tells the caller to keep the content in the chat itself.
/// </remarks>
public sealed class NoProvenanceStoreClient(string name, string? unavailableReason, DatabaseClientStatus status = DatabaseClientStatus.UNAVAILABLE) : ProvenanceStoreClient(name, string.Empty)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(NoProvenanceStoreClient).Namespace, nameof(NoProvenanceStoreClient));

    public override DatabaseClientStatus Status => status;

    public override async IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo()
    {
        yield return (TB("Status"), status switch
        {
            DatabaseClientStatus.STARTING => TB("Starting"),
            _ => TB("Unavailable")
        });

        if (!string.IsNullOrWhiteSpace(unavailableReason))
            yield return (TB("Reason"), unavailableReason);

        // As in NoIndexStoreClient: which library this process bound to matters most when SQLite cannot be loaded at all.
        yield return (TB("Native library"), OrUnknown(SqliteRuntimeInfo.GetNativeLibraryName()));
        yield return (TB("Process architecture"), OrUnknown(SqliteRuntimeInfo.GetProcessArchitecture()));

        await Task.CompletedTask;
    }

    private static string OrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? TB("unknown") : value;

    public override Task<Guid?> StoreAsync(Guid chatId, ProvenanceKind kind, string content, IReadOnlyList<string>? surroundingContent, CancellationToken token) => Task.FromResult<Guid?>(null);

    public override Task<IReadOnlyDictionary<Guid, ProvenanceEntry>> GetEntriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult<IReadOnlyDictionary<Guid, ProvenanceEntry>>(new Dictionary<Guid, ProvenanceEntry>());

    public override Task CopyChatAsync(Guid sourceChatId, Guid targetChatId, CancellationToken token) => Task.CompletedTask;

    public override Task<int> DeleteChatAsync(Guid chatId, CancellationToken token, DateTimeOffset? createdBefore = null) => Task.FromResult(0);

    public override Task<int> DeleteChatEntriesExceptAsync(Guid chatId, IReadOnlyCollection<Guid> keptIds, CancellationToken token, DateTimeOffset? createdBefore = null) => Task.FromResult(0);

    public override Task<int> DeleteEntriesAsync(Guid chatId, IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(0);

    public override Task<IReadOnlyList<Guid>> GetChatIdsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Guid>>([]);

    /// <inheritdoc />
    /// <remarks>
    /// Unlike the other methods, this one does not pretend success: a backup would take an empty
    /// copy for the real one. Whoever makes the backup decides what to do instead.
    /// </remarks>
    public override Task<string> BackupToDirectoryAsync(string targetDirectory, CancellationToken token) =>
        Task.FromException<string>(new InvalidOperationException(unavailableReason ?? "The provenance store is not available."));

    public override Task<int> ImportChatAsync(string sourceDatabasePath, Guid sourceChatId, Guid targetChatId, CancellationToken token) => Task.FromResult(0);

    public override void Dispose()
    {
    }
}