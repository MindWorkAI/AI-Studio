using AIStudio.Chat;
using AIStudio.Chat.Embedded;

namespace AIStudio.Tests.Chat.Embedded;

/// <summary>
/// Keeps embedded chats in memory, as they were when they were stored last.
/// </summary>
public sealed class InMemoryEmbeddedChatStore : IEmbeddedChatStore
{
    private readonly Dictionary<Guid, ChatThread> threads = [];

    /// <summary>
    /// Whether loading fails, as with a chat this version cannot open.
    /// </summary>
    public bool CannotOpen { get; init; }

    /// <summary>
    /// How often a chat was stored.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Gets the blocks of a chat as they were stored last, or none.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    public IReadOnlyList<ContentBlock> StoredBlocks(Guid chatId) => this.threads.TryGetValue(chatId, out var thread) ? thread.Blocks : [];

    /// <inheritdoc />
    public Task<ChatThread?> LoadThreadAsync(Guid chatId, CancellationToken token = default)
    {
        if (this.CannotOpen)
            throw new InvalidDataException("A newer version stored this chat.");

        return Task.FromResult(this.threads.TryGetValue(chatId, out var thread) ? thread : null);
    }

    /// <inheritdoc />
    public Task SaveThreadAsync(ChatThread thread, CancellationToken token = default)
    {
        // A copy of the list, so that what the chat adds later does not change what was stored:
        this.threads[thread.ChatId] = thread with { Blocks = [.. thread.Blocks] };
        this.SaveCount++;
        return Task.CompletedTask;
    }
}