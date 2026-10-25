using AIStudio.Tools.Databases.ProvenanceStore;

namespace AIStudio.Chat;

/// <summary>
/// Removes from the provenance store what no stored chat refers to anymore.
/// </summary>
/// <remarks>
/// Deleting a chat takes its entries along right away, see ChatProvenance, and so does the
/// maintenance of the temporary chats. What is left over comes from changes inside a chat, or from
/// runs which ended before they could clean up:
/// <list type="bullet">
/// <item>a message which was removed, edited, or answered anew, together with its passages and attached documents;</item>
/// <item>a text which a preview stored, while its chat was never stored with it, or sending could not remove it;</item>
/// <item>an assistant whose conversation was never sent to a chat;</item>
/// <item>a chat which was never stored, or deleted while the store was not available.</item>
/// </list>
/// Each chat file is read for its references, which only a cleanup at the start can do safely:
/// while the app runs, a chat in memory may refer to an entry its file does not know yet. For the
/// same reason, only entries stored before the start are removed.<br/><br/>
///
/// Reading every chat at every start would cost much on a small computer, so a chat is read only
/// when something changed since the last complete cleanup: its file was written since, or it
/// received an entry since. Otherwise, the last cleanup removed all it could, and nothing it kept
/// has become unreferenced. A chat which is not read keeps its entries; skipping never removes
/// anything. Chats which no longer exist cost no reading and are always taken care of.
/// </remarks>
public static class ProvenanceCleanup
{
    /// <summary>
    /// How long before the last cleanup a chat file may have been written and still counts as changed.
    /// </summary>
    /// <remarks>
    /// The time of a file may be coarse, e.g., two seconds on FAT, or come from the clock of another
    /// computer, for a data folder on a network drive. Reading a few chats too many costs little,
    /// while missing one leaves what it no longer refers to until it changes again.
    /// </remarks>
    private static readonly TimeSpan WRITE_TIME_TOLERANCE = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Removes the entries of chats which no longer exist, and those their chat no longer refers to.
    /// </summary>
    /// <param name="store">The provenance store.</param>
    /// <param name="chatLocations">The workspace of every chat on the disk by its ID, Guid.Empty for a temporary chat.</param>
    /// <param name="loadChat">Reads a chat by its workspace and ID, or answers null when it cannot be read.</param>
    /// <param name="getChatWrittenAt">Tells when a chat file was last written, by its workspace and ID, or answers null when that is not known.</param>
    /// <param name="createdBefore">The start of this run: entries stored since may belong to a chat which is not on the disk yet.</param>
    /// <param name="lastCleanup">When the last complete cleanup began, or null to read every chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What the cleanup did.</returns>
    public static async Task<ProvenanceCleanupResult> CleanUpAsync(ProvenanceStoreClient store, IReadOnlyDictionary<Guid, Guid> chatLocations, Func<Guid, Guid, Task<ChatThread?>> loadChat, Func<Guid, Guid, DateTimeOffset?> getChatWrittenAt, DateTimeOffset createdBefore, DateTimeOffset? lastCleanup, CancellationToken token)
    {
        // The chats which received an entry since the last cleanup, in one query, which the last cleanup had to leave alone:
        var chatsWithNewEntries = lastCleanup is { } since ? (await store.GetChatIdsAsync(token, since)).ToHashSet() : [];

        var unreferenced = 0;
        var skipped = 0;
        var unreadable = 0;
        var orphanedChats = new List<Guid>();
        foreach (var chatId in await store.GetChatIdsAsync(token))
        {
            token.ThrowIfCancellationRequested();
            if (!chatLocations.TryGetValue(chatId, out var workspaceId))
            {
                orphanedChats.Add(chatId);
                continue;
            }

            // A chat whose time of writing is not known counts as changed:
            if (lastCleanup is { } previousStart && !chatsWithNewEntries.Contains(chatId) && getChatWrittenAt(workspaceId, chatId) is { } writtenAt && writtenAt < previousStart - WRITE_TIME_TOLERANCE)
            {
                skipped++;
                continue;
            }

            // A chat which cannot be read keeps everything: nothing tells what it refers to.
            var chat = await loadChat(workspaceId, chatId);
            if (chat is null)
            {
                unreadable++;
                continue;
            }

            unreferenced += await store.DeleteChatEntriesExceptAsync(chatId, chat.CollectProvenanceIds(), token, createdBefore);
        }

        // The chats which no longer exist all at once, since there may be many of them after a long time without a start:
        var orphaned = await store.DeleteChatsAsync(orphanedChats, token, createdBefore);
        return new(orphaned, unreferenced, skipped, unreadable);
    }
}