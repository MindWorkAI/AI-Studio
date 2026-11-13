namespace AIStudio.Chat.Embedded;

/// <summary>
/// Where the embedded chats of an assistant live, e.g., in a folder of the assistant below the data directory.
/// </summary>
/// <remarks>
/// An embedded chat never lives in a workspace: it belongs to the assistant which shows it, and the assistant decides
/// how long it stays. The store finds a chat by its ID, ChatThread.ChatId.
/// </remarks>
public interface IEmbeddedChatStore
{
    /// <summary>
    /// Loads a chat.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The chat, or null when it has no stored thread yet.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the stored chat; the file stays as it is.</exception>
    public Task<ChatThread?> LoadThreadAsync(Guid chatId, CancellationToken token = default);

    /// <summary>
    /// Stores a chat, in place of what was stored of it before.
    /// </summary>
    /// <param name="thread">The chat, with its ID.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="FileNotFoundException">What the chat belonged to was deleted meanwhile; nothing is left to store.</exception>
    public Task SaveThreadAsync(ChatThread thread, CancellationToken token = default);
}