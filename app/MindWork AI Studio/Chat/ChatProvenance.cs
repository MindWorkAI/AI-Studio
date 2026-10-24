using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;

namespace AIStudio.Chat;

/// <summary>
/// Keeps what chats received in the provenance store in step with the chats themselves: it goes
/// when its chat goes, and it comes along when its chat is copied.
/// </summary>
/// <remarks>
/// Called from the places which delete or copy chats. Nothing here may stop them: a chat is
/// deleted or copied either way, and what the store keeps too long is removed by the cleanup at
/// the next start, see ProvenanceCleanup.
/// </remarks>
public static class ChatProvenance
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(ChatProvenance));

    /// <summary>
    /// Collects the entries of the provenance store which a chat refers to.
    /// </summary>
    /// <remarks>
    /// The passages of its answers, the documents attached to its messages, and those attached to
    /// the message being written, whose text a preview may have stored already.
    /// </remarks>
    /// <param name="thread">The chat.</param>
    /// <returns>The ids of the entries.</returns>
    public static IReadOnlySet<Guid> CollectProvenanceIds(this ChatThread thread)
    {
        var provenanceIds = new HashSet<Guid>();
        foreach (var block in thread.Blocks)
        {
            if (block.Content is ContentText text)
                provenanceIds.UnionWith(text.SourcePassages.Select(passage => passage.ProvenanceId).OfType<Guid>());

            if (block.Content is not null)
                provenanceIds.UnionWith(block.Content.FileAttachments.Select(attachment => attachment.ProvenanceId).OfType<Guid>());
        }

        provenanceIds.UnionWith(thread.Draft.FileAttachments.Select(attachment => attachment.ProvenanceId).OfType<Guid>());
        return provenanceIds;
    }

    /// <summary>
    /// Deletes what the given chats received from the provenance store, for chats which were deleted.
    /// </summary>
    /// <param name="chatIds">The deleted chats.</param>
    public static async Task ForgetChatsAsync(IEnumerable<Guid> chatIds)
    {
        if (await GetStoreAsync() is not { } store)
            return;

        foreach (var chatId in chatIds)
        {
            try
            {
                var deleted = await store.DeleteChatAsync(chatId, CancellationToken.None);
                if (deleted > 0)
                    LOGGER.LogInformation("Deleted {Count} provenance entries of the deleted chat '{ChatId}'.", deleted, chatId);
            }
            catch (Exception exception)
            {
                LOGGER.LogWarning(exception, "Failed to delete the provenance entries of the deleted chat '{ChatId}'. The next start removes them.", chatId);
            }
        }
    }

    /// <summary>
    /// Copies what a chat received to its copy, which refers to the same entries.
    /// </summary>
    /// <param name="sourceChatId">The chat which was copied.</param>
    /// <param name="targetChatId">The copy.</param>
    public static async Task CopyChatAsync(Guid sourceChatId, Guid targetChatId)
    {
        if (await GetStoreAsync() is not { } store)
            return;

        try
        {
            await store.CopyChatAsync(sourceChatId, targetChatId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            LOGGER.LogWarning(exception, "Failed to copy the provenance entries of the chat '{SourceChatId}' to its copy '{TargetChatId}'. The copy shows them as long as the original keeps them.", sourceChatId, targetChatId);
        }
    }

    /// <summary>
    /// Gets the provenance store, or nothing when there is none to keep in step, e.g., in a test without the app's services.
    /// </summary>
    private static async Task<ProvenanceStoreClient?> GetStoreAsync()
    {
        try
        {
            if (Program.SERVICE_PROVIDER?.GetService<DatabaseClientProvider>() is not { } databaseClientProvider)
                return null;

            var store = await databaseClientProvider.GetProvenanceStoreAsync();
            return store is NoProvenanceStoreClient ? null : store;
        }
        catch (Exception exception)
        {
            LOGGER.LogWarning(exception, "The provenance store is not available to keep in step with the chats.");
            return null;
        }
    }
}