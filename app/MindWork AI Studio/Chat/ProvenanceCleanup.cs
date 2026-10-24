using AIStudio.Tools.Databases.ProvenanceStore;

namespace AIStudio.Chat;

/// <summary>
/// Removes from the provenance store what no stored chat refers to anymore.
/// </summary>
/// <remarks>
/// Deleting a chat takes its entries along right away, see ChatProvenance. What is left over
/// comes from changes inside a chat, or from runs which ended before they could clean up:
/// <list type="bullet">
/// <item>a message which was removed, edited, or answered anew, together with its passages and attached documents;</item>
/// <item>a text which a preview stored, while its chat was never stored with it, or sending could not remove it;</item>
/// <item>an assistant whose conversation was never sent to a chat;</item>
/// <item>a temporary chat which the maintenance deleted: it may still be open, and storing it again brings it back;</item>
/// <item>a chat which was never stored, or deleted while the store was not available.</item>
/// </list>
/// Each chat file is read for its references, which only a cleanup at the start can do safely:
/// while the app runs, a chat in memory may refer to an entry its file does not know yet. For the
/// same reason, only entries stored before the start are removed.
/// </remarks>
public static class ProvenanceCleanup
{
    /// <summary>
    /// Removes the entries of chats which no longer exist, and those their chat no longer refers to.
    /// </summary>
    /// <param name="store">The provenance store.</param>
    /// <param name="chatLocations">The workspace of every chat on the disk by its ID, Guid.Empty for a temporary chat.</param>
    /// <param name="loadChat">Reads a chat by its workspace and ID, or answers null when it cannot be read.</param>
    /// <param name="createdBefore">The start of this run: entries stored since may belong to a chat which is not on the disk yet.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many entries of chats which no longer exist were removed, and how many of chats which no longer refer to them.</returns>
    public static async Task<(int Orphaned, int Unreferenced)> CleanUpAsync(ProvenanceStoreClient store, IReadOnlyDictionary<Guid, Guid> chatLocations, Func<Guid, Guid, Task<ChatThread?>> loadChat, DateTimeOffset createdBefore, CancellationToken token)
    {
        var orphaned = 0;
        var unreferenced = 0;
        foreach (var chatId in await store.GetChatIdsAsync(token))
        {
            token.ThrowIfCancellationRequested();
            if (!chatLocations.TryGetValue(chatId, out var workspaceId))
            {
                orphaned += await store.DeleteChatAsync(chatId, token, createdBefore);
                continue;
            }

            // A chat which cannot be read keeps everything: nothing tells what it refers to.
            var chat = await loadChat(workspaceId, chatId);
            if (chat is null)
                continue;

            unreferenced += await store.DeleteChatEntriesExceptAsync(chatId, chat.CollectProvenanceIds(), token, createdBefore);
        }

        return (orphaned, unreferenced);
    }
}