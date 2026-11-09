namespace AIStudio.Components;

/// <summary>
/// Whether the open chat holds changes the user did not save yet, which the chat and the workspace
/// sidebar share.
/// </summary>
/// <remarks>
/// Only while the user stores chats by clicking save: the sidebar then shows the open chat with a mark
/// and a save button, also a new chat which was never saved. The chat page owns the state and hands it
/// to both. It also outlives the chat component, which the page creates anew whenever the sidebar shows
/// up again, so the new component knows what the old one did not save.
/// </remarks>
public sealed class ChatSaveState
{
    /// <summary>
    /// The chat whose changes are not saved yet, or <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid ChatId { get; private set; }

    /// <summary>
    /// Whether the open chat holds changes the user did not save yet, while chats are saved by hand.
    /// </summary>
    public bool HasUnsavedChanges { get; private set; }

    /// <summary>
    /// Tells the sidebar that the open chat got unsaved changes, or that the user saved them.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Whether the given chat holds changes the user did not save yet.
    /// </summary>
    /// <param name="chatId">The chat.</param>
    public bool IsUnsaved(Guid chatId) => this.HasUnsavedChanges && chatId != Guid.Empty && this.ChatId == chatId;

    /// <summary>
    /// Notes whether the open chat holds unsaved changes.
    /// </summary>
    /// <param name="chatId">The open chat.</param>
    /// <param name="hasUnsavedChanges">Whether it holds changes the user did not save yet.</param>
    public void SetUnsavedChanges(Guid chatId, bool hasUnsavedChanges)
    {
        if (this.ChatId == chatId && this.HasUnsavedChanges == hasUnsavedChanges)
            return;

        this.ChatId = chatId;
        this.HasUnsavedChanges = hasUnsavedChanges;
        this.Changed?.Invoke();
    }
}