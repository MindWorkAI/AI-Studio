namespace AIStudio.Chat;

/// <summary>
/// Stores a chat shortly after the user changed its draft, and once more when the user leaves it.
/// </summary>
/// <remarks>
/// The message the user has not sent yet travels in the draft of its chat, so storing the chat is what
/// keeps the message. Storing after every keystroke would write the whole conversation each time,
/// which is why this waits until the user paused for a moment. Every change starts the waiting anew,
/// by moving the one timer instead of starting another one.<br/><br/>
///
/// Only a chat which is stored already gets stored here. Putting a new chat on the disk is up to whoever
/// created it, and a chat which is gone from the disk was deleted: storing it would bring it back.<br/><br/>
///
/// While an answer is being written for the chat, nothing is stored here either. The AI job changes the
/// conversation and stores it on its own, with the draft, since it holds the very same draft. Storing
/// the chat from here at the same time could read the conversation in the middle of a change.<br/><br/>
///
/// Storing always runs on the thread pool, never on the renderer, because it writes the whole
/// conversation to the disk.
/// </remarks>
/// <param name="delay">How long the user has to pause before the chat is stored.</param>
/// <param name="isStored">Whether the chat is stored on the disk.</param>
/// <param name="isBusy">Whether an answer is being written for the chat right now.</param>
/// <param name="storeAsync">Stores the chat, and answers false when that was skipped.</param>
/// <param name="logger">Where failures are reported.</param>
public sealed class ChatDraftAutosave(TimeSpan delay, Func<ChatThread, bool> isStored, Func<ChatThread, bool> isBusy, Func<ChatThread, Task<bool>> storeAsync, ILogger logger) : IAsyncDisposable
{
    private readonly Lock stateLock = new();
    private Timer? timer;
    private ChatThread? pendingChat;
    private bool isDisposed;

    /// <summary>
    /// Notes that the user changed the draft of the given chat, which gets stored once the user paused.
    /// </summary>
    /// <param name="chat">The chat as the composer shows it now.</param>
    public void Schedule(ChatThread chat)
    {
        ChatThread? previousChat;
        lock (this.stateLock)
        {
            if (this.isDisposed)
                return;

            previousChat = this.pendingChat is { } pending && pending.ChatId != chat.ChatId ? pending : null;
            this.pendingChat = chat;
            this.timer ??= new Timer(this.WhenDelayElapsed);
            this.timer.Change(delay, Timeout.InfiniteTimeSpan);
        }

        //
        // Whoever opens another chat flushes before, so this is a safety net only: a change in one chat
        // must not cost another chat its draft.
        //
        if (previousChat is not null)
            this.StoreWhenLeavingAsync(previousChat).Observe($"{nameof(ChatDraftAutosave)}: storing the draft of a chat left without a flush");
    }

    /// <summary>
    /// Swaps the noted chat for a newer copy of the same chat.
    /// </summary>
    /// <remarks>
    /// Some changes replace the thread with a copy, e.g., picking another profile. The copy shares the
    /// draft, but storing the former thread would bring the former profile back.
    /// </remarks>
    /// <param name="chat">The copy which replaced the thread.</param>
    public void Follow(ChatThread chat)
    {
        lock (this.stateLock)
        {
            if (this.pendingChat?.ChatId == chat.ChatId)
                this.pendingChat = chat;
        }
    }

    /// <summary>
    /// Stores the noted chat right away, which is what leaving the chat calls for.
    /// </summary>
    public async Task FlushAsync()
    {
        ChatThread? chat;
        lock (this.stateLock)
        {
            this.timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            chat = this.pendingChat;
            this.pendingChat = null;
        }

        if (chat is not null)
            await this.StoreWhenLeavingAsync(chat);
    }

    private Task StoreWhenLeavingAsync(ChatThread chat) => Task.Run(async () =>
    {
        //
        // A chat being answered is left to the AI job, which stores it once more when the answer is
        // done, and the draft with it:
        //
        if (isBusy(chat) || !isStored(chat))
            return;

        await this.TryStoreAsync(chat);
    });

    private void WhenDelayElapsed(object? state) => this.StoreAfterPauseAsync().Observe($"{nameof(ChatDraftAutosave)}: storing a draft after a pause");

    private async Task StoreAfterPauseAsync()
    {
        ChatThread? chat;
        lock (this.stateLock)
            chat = this.isDisposed ? null : this.pendingChat;

        if (chat is null)
            return;

        if (isBusy(chat))
        {
            lock (this.stateLock)
            {
                if (!this.isDisposed && ReferenceEquals(this.pendingChat, chat))
                    this.timer?.Change(delay, Timeout.InfiniteTimeSpan);
            }

            return;
        }

        if (!isStored(chat))
            return;

        lock (this.stateLock)
        {
            // Another change came in meanwhile, and its own timer runs already:
            if (!ReferenceEquals(this.pendingChat, chat))
                return;

            this.pendingChat = null;
        }

        if (await this.TryStoreAsync(chat))
            return;

        //
        // Noted again for the next change or for leaving the chat, but without a timer: when the disk
        // fails, trying again every few seconds would only fill the log.
        //
        lock (this.stateLock)
        {
            if (!this.isDisposed)
                this.pendingChat ??= chat;
        }
    }

    private async Task<bool> TryStoreAsync(ChatThread chat)
    {
        try
        {
            if (await storeAsync(chat))
                return true;

            logger.LogWarning("Storing the draft of the chat '{ChatId}' was skipped, because another operation held the chat.", chat.ChatId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "The draft of the chat '{ChatId}' could not be stored.", chat.ChatId);
        }

        return false;
    }

    #region Implementation of IAsyncDisposable

    /// <summary>
    /// Stores what is still noted, and stops the timer.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (this.stateLock)
            this.isDisposed = true;

        await this.FlushAsync();

        Timer? disposedTimer;
        lock (this.stateLock)
        {
            disposedTimer = this.timer;
            this.timer = null;
        }

        if (disposedTimer is not null)
            await disposedTimer.DisposeAsync();
    }

    #endregion
}