namespace AIStudio.Chat;

/// <summary>
/// Keeps the new chats which live in memory: those of the places which do not store their chats automatically.
/// </summary>
/// <remarks>
/// Every place, the chats outside of workspaces and every workspace, has at most one new chat, see
/// ChatThread.IsNewChat. Where chats are stored automatically, the new chat is stored like any other
/// chat. Anywhere else, it lives here while AI Studio runs, so that the user finds it again after
/// visiting other chats or pages: where chats are saved by hand, also after the user saved it, since
/// changes made afterward are not saved yet; where chats are never stored, until AI Studio ends.<br/><br/>
///
/// One instance serves the whole app, since the chat page is created anew whenever the user comes
/// back to it. The chats are the very threads the chat page works with, so a transcript which arrives
/// while the user is elsewhere lands in the chat which the list shows.
/// </remarks>
public sealed class NewChatRegistry
{
    private readonly Lock stateLock = new();
    private readonly Dictionary<Guid, Entry> entriesByPlace = [];

    /// <summary>
    /// The new chat of a place, and whether it holds changes the user did not save yet.
    /// </summary>
    private sealed class Entry(ChatThread chat)
    {
        public ChatThread Chat { get; set; } = chat;

        public bool HasUnsavedChanges { get; set; }
    }

    /// <summary>
    /// The new chat of the given place, when it lives in memory.
    /// </summary>
    /// <param name="workspaceId">The workspace, or Guid.Empty for the chats outside of workspaces.</param>
    /// <returns>The chat, or null.</returns>
    public ChatThread? Find(Guid workspaceId)
    {
        lock (this.stateLock)
            return this.entriesByPlace.TryGetValue(workspaceId, out var entry) ? entry.Chat : null;
    }

    /// <summary>
    /// Whether the given chat is a new chat which lives in memory.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    public bool Contains(Guid chatId)
    {
        lock (this.stateLock)
            return this.entriesByPlace.Values.Any(entry => entry.Chat.ChatId == chatId);
    }

    /// <summary>
    /// Whether the given new chat holds changes the user did not save yet, which the list shows while chats are saved by hand.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    public bool HasUnsavedChanges(Guid chatId)
    {
        lock (this.stateLock)
            return this.entriesByPlace.Values.Any(entry => entry.Chat.ChatId == chatId && entry.HasUnsavedChanges);
    }

    /// <summary>
    /// The new chats in memory, of all places.
    /// </summary>
    /// <returns>Every chat, with whether it holds unsaved changes.</returns>
    public IReadOnlyList<(ChatThread Chat, bool HasUnsavedChanges)> GetAll()
    {
        lock (this.stateLock)
            return this.entriesByPlace.Values.Select(entry => (entry.Chat, entry.HasUnsavedChanges)).ToList();
    }

    /// <summary>
    /// Keeps the given new chat as the new chat of its place.
    /// </summary>
    /// <remarks>
    /// A newer thread of the same chat takes the place of the one kept before, e.g., after the user
    /// picked another profile, which replaces the thread with a copy. Whether it holds unsaved changes
    /// stays as it was.
    /// </remarks>
    /// <param name="chat">The new chat.</param>
    public void Keep(ChatThread chat)
    {
        lock (this.stateLock)
        {
            if (this.entriesByPlace.TryGetValue(chat.WorkspaceId, out var entry) && entry.Chat.ChatId == chat.ChatId)
                entry.Chat = chat;
            else
                this.entriesByPlace[chat.WorkspaceId] = new Entry(chat);
        }
    }

    /// <summary>
    /// Swaps a kept chat for a newer thread of the same chat, and does nothing for any other chat.
    /// </summary>
    /// <param name="chat">The newer thread.</param>
    public void Follow(ChatThread chat)
    {
        lock (this.stateLock)
        {
            if (this.entriesByPlace.TryGetValue(chat.WorkspaceId, out var entry) && entry.Chat.ChatId == chat.ChatId)
                entry.Chat = chat;
        }
    }

    /// <summary>
    /// Notes whether a kept chat holds changes the user did not save yet.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    /// <param name="hasUnsavedChanges">Whether it holds unsaved changes.</param>
    public void SetUnsavedChanges(Guid chatId, bool hasUnsavedChanges)
    {
        lock (this.stateLock)
        {
            foreach (var entry in this.entriesByPlace.Values.Where(entry => entry.Chat.ChatId == chatId))
                entry.HasUnsavedChanges = hasUnsavedChanges;
        }
    }

    /// <summary>
    /// Forgets the given chat, since it is no new chat any longer: the user sent it, emptied it, or deleted it.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    public void Forget(Guid chatId)
    {
        lock (this.stateLock)
        {
            foreach (var place in this.entriesByPlace.Where(pair => pair.Value.Chat.ChatId == chatId).Select(pair => pair.Key).ToList())
                this.entriesByPlace.Remove(place);
        }
    }

    /// <summary>
    /// Forgets the new chat of the given place, since the place is gone together with its chats.
    /// </summary>
    /// <param name="workspaceId">The workspace which was deleted.</param>
    public void ForgetPlace(Guid workspaceId)
    {
        lock (this.stateLock)
            this.entriesByPlace.Remove(workspaceId);
    }
}