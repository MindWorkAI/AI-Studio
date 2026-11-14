using AIStudio.Chat;
using AIStudio.Chat.Embedded;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Lets the embedded chat of a place of the BeyondFacts Lab live where the lab keeps it, e.g., in the folder of a step
/// of a preparation.
/// </summary>
/// <remarks>
/// The ID of a chat of the lab follows from its place, see BflThreadId, so the store serves the one chat of its place
/// and refuses any other. Files, paths, and locks stay those of BeyondFactsLabStore, so chats stored before keep
/// working, and the cleanup of the provenance store still finds every chat, see
/// BeyondFactsLabStore.ReadAllThreadLocations.
/// </remarks>
/// <param name="store">The store of the lab.</param>
/// <param name="location">The place of the chat.</param>
public sealed class BflChatStore(BeyondFactsLabStore store, BflThreadLocation location) : IEmbeddedChatStore
{
    /// <inheritdoc />
    public Task<ChatThread?> LoadThreadAsync(Guid chatId, CancellationToken token = default) => store.LoadThreadAsync(this.LocationOf(chatId), token);

    /// <inheritdoc />
    public Task SaveThreadAsync(ChatThread thread, CancellationToken token = default) => store.SaveThreadAsync(this.LocationOf(thread.ChatId), thread, token);

    /// <inheritdoc />
    public Task DeleteThreadAsync(Guid chatId, CancellationToken token = default) => store.DeleteThreadAsync(this.LocationOf(chatId), token);

    /// <summary>
    /// Gets the place of a chat, which must be the place of this store.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    /// <returns>The place of the store.</returns>
    /// <exception cref="ArgumentException">The chat belongs to another place.</exception>
    private BflThreadLocation LocationOf(Guid chatId) => chatId == location.ChatId
        ? location
        : throw new ArgumentException("The chat belongs to another place of the lab.", nameof(chatId));
}