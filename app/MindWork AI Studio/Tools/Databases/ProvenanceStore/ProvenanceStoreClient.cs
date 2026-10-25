namespace AIStudio.Tools.Databases.ProvenanceStore;

/// <summary>
/// Keeps what chats received besides their messages: the content of attached files, the results
/// of tools, and the passages of data sources, each exactly as the AI received it.
/// </summary>
/// <remarks>
/// A chat keeps only the ids of its entries, next to where each one came from. The content lives
/// here, so that a chat file stays small and an attached file is read only once.<br/><br/>
///
/// A database of its own rather than a table of the index: the index of the data sources can be
/// rebuilt from the files and is treated that way, e.g., a backup may leave it out. What a chat
/// received cannot be rebuilt, since the files, pages and mails may have changed since.<br/><br/>
///
/// Every entry belongs to one chat. The same id may stand for an entry of several chats, after a
/// chat was copied, and then it holds the same content in each.
/// </remarks>
public abstract class ProvenanceStoreClient(string name, string path) : DatabaseClient(name, path)
{
    /// <summary>
    /// Stores one piece of content a chat received.
    /// </summary>
    /// <param name="chatId">The chat which received the content.</param>
    /// <param name="kind">What sort of content it is.</param>
    /// <param name="content">The content, exactly as the AI received it.</param>
    /// <param name="surroundingContent">The content an ERI server sent along around its passage, or nothing.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The id of the new entry, or null when this store cannot keep anything right now.</returns>
    public abstract Task<Guid?> StoreAsync(Guid chatId, ProvenanceKind kind, string content, IReadOnlyList<string>? surroundingContent, CancellationToken token);

    /// <summary>
    /// Reads entries by their ids.
    /// </summary>
    /// <remarks>
    /// By the ids alone, without the chat: the place which shows an entry seldom knows its chat,
    /// and every copy of an entry holds the same content.
    /// </remarks>
    /// <param name="ids">The ids to read.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The entries found, by their ids. An id without an entry is missing from it.</returns>
    public abstract Task<IReadOnlyDictionary<Guid, ProvenanceEntry>> GetEntriesAsync(IReadOnlyCollection<Guid> ids, CancellationToken token);

    /// <summary>
    /// Copies every entry of a chat to another chat, keeping their ids.
    /// </summary>
    /// <remarks>
    /// The copy is what a chat made from another one refers to. Entries the target holds already
    /// stay as they are.
    /// </remarks>
    /// <param name="sourceChatId">The chat to copy from.</param>
    /// <param name="targetChatId">The chat to copy to.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task CopyChatAsync(Guid sourceChatId, Guid targetChatId, CancellationToken token);

    /// <summary>
    /// Deletes every entry of some chats.
    /// </summary>
    /// <remarks>
    /// All of them at once: deleting a workspace or cleaning up at the start may concern many chats.
    /// </remarks>
    /// <param name="chatIds">The chats.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="createdBefore">Deletes only the entries stored before this moment, or all of them when null.</param>
    /// <returns>How many entries were deleted.</returns>
    public abstract Task<int> DeleteChatsAsync(IReadOnlyCollection<Guid> chatIds, CancellationToken token, DateTimeOffset? createdBefore = null);

    /// <summary>
    /// Deletes the entries of a chat which it no longer refers to.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    /// <param name="keptIds">The ids the chat still refers to.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="createdBefore">Deletes only the entries stored before this moment, or all of them when null.</param>
    /// <returns>How many entries were deleted.</returns>
    public abstract Task<int> DeleteChatEntriesExceptAsync(Guid chatId, IReadOnlyCollection<Guid> keptIds, CancellationToken token, DateTimeOffset? createdBefore = null);

    /// <summary>
    /// Deletes some entries of a chat, e.g., texts which a preview kept for a message that went without them.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    /// <param name="ids">The ids of the entries.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many entries were deleted.</returns>
    public abstract Task<int> DeleteEntriesAsync(Guid chatId, IReadOnlyCollection<Guid> ids, CancellationToken token);

    /// <summary>
    /// Lists the chats this store holds entries of.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <param name="createdSince">Lists only the chats with an entry stored at or after this moment, or all of them when null.</param>
    /// <returns>The ids of the chats, Guid.Empty included when entries were stored for a chat without an id.</returns>
    public abstract Task<IReadOnlyList<Guid>> GetChatIdsAsync(CancellationToken token, DateTimeOffset? createdSince = null);

    /// <summary>
    /// Reads when the last complete cleanup of this store began, see ProvenanceCleanup.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The moment, or null when no cleanup was noted, e.g., for a store a restore brought back.</returns>
    public abstract Task<DateTimeOffset?> GetLastCleanupAsync(CancellationToken token);

    /// <summary>
    /// Notes when a complete cleanup of this store began, see ProvenanceCleanup.
    /// </summary>
    /// <param name="startedAt">The moment the cleanup began.</param>
    /// <param name="token">The cancellation token.</param>
    public abstract Task SetLastCleanupAsync(DateTimeOffset startedAt, CancellationToken token);

    /// <summary>
    /// Writes a copy of this store into a directory, for a backup.
    /// </summary>
    /// <param name="targetDirectory">The directory of the copy. A copy written there before is replaced.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The path of the copy.</returns>
    public abstract Task<string> BackupToDirectoryAsync(string targetDirectory, CancellationToken token);

    /// <summary>
    /// Adds the entries of one chat from a copy of this store, such as the one a backup holds.
    /// </summary>
    /// <remarks>
    /// The entries keep their ids, so the chat's references stay valid, also when the chat is added
    /// under another id. Entries the target chat holds already stay as they are.
    /// </remarks>
    /// <param name="sourceDatabasePath">The copy to read from. It is only read.</param>
    /// <param name="sourceChatId">The chat in the copy.</param>
    /// <param name="targetChatId">The chat in this store.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many entries were added.</returns>
    public abstract Task<int> ImportChatAsync(string sourceDatabasePath, Guid sourceChatId, Guid targetChatId, CancellationToken token);
}