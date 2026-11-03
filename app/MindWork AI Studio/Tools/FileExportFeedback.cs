namespace AIStudio.Tools;

/// <summary>
/// Tells the user that a file is being exported, and afterward where it went.
/// </summary>
/// <remarks>
/// Start it once the user chose the file, and dispose of it when the export is over, also after an error:
/// disposing removes the notification that the export runs, in case it still stands. That notification
/// appears only after a moment, so that a fast export shows its success right away instead of two
/// notifications which flicker one after the other.
/// <br/><br/>
/// The notification that the export runs must never appear after its end, where it would stay until the
/// user closes it. The delayed notification and the end of the export therefore take the same lock, and
/// whichever comes second sees what the first one did. That settles the order in which they are sent;
/// FileExportNotifications settles the order in which they take effect.
/// </remarks>
public sealed class FileExportFeedback : IAsyncDisposable
{
    /// <summary>
    /// How long an export runs before the user hears about it.
    /// </summary>
    private static readonly TimeSpan RUNNING_MESSAGE_DELAY = TimeSpan.FromMilliseconds(500);

    private readonly Func<Event, object, Task> send;
    private readonly Func<CancellationToken, Task> waitForRunningMessage;
    private readonly string key = Guid.NewGuid().ToString("N");
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly CancellationTokenSource runningMessageCancellation = new();

    private DataFileExportRunningMessage runningMessage;
    private Task delayedRunningMessage = Task.CompletedTask;
    private bool isRunningMessageShown;
    private bool isFinished;

    public FileExportFeedback() : this((triggeredEvent, data) => MessageBus.INSTANCE.SendMessage(null, triggeredEvent, data), token => Task.Delay(RUNNING_MESSAGE_DELAY, token))
    {
    }

    /// <summary>
    /// Creates the feedback with what it needs from outside, so that tests can stand in for both.
    /// </summary>
    /// <param name="send">Sends a notification, as the message bus does.</param>
    /// <param name="waitForRunningMessage">Waits until the notification that the export runs is due.</param>
    internal FileExportFeedback(Func<Event, object, Task> send, Func<CancellationToken, Task> waitForRunningMessage)
    {
        this.send = send;
        this.waitForRunningMessage = waitForRunningMessage;
    }

    /// <summary>
    /// Shows that the given file is being exported, unless the export ends before that is due.
    /// </summary>
    /// <param name="filePath">The file the export writes.</param>
    public void ShowRunning(string filePath)
    {
        this.runningMessage = new DataFileExportRunningMessage(this.key, filePath);
        this.delayedRunningMessage = this.ShowRunningWhenDueAsync(this.runningMessageCancellation.Token);
    }

    /// <summary>
    /// Ends the export with the notification of its success, which replaces the one that it runs.
    /// </summary>
    /// <param name="icon">The icon of the notification.</param>
    /// <param name="message">What was exported.</param>
    /// <param name="filePath">The file which was written.</param>
    /// <param name="offersOpening">Whether the file may be opened with the default program.</param>
    public Task SucceededAsync(string icon, string message, string filePath, bool offersOpening) => this.FinishAsync(new DataFileExportSuccessMessage(this.key, icon, message, filePath, offersOpening));

    private async Task ShowRunningWhenDueAsync(CancellationToken token)
    {
        try
        {
            await this.waitForRunningMessage(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await this.sendLock.WaitAsync();
        try
        {
            if (this.isFinished)
                return;

            await this.send(Event.SHOW_FILE_EXPORT_RUNNING, this.runningMessage);
            this.isRunningMessageShown = true;
        }
        finally
        {
            this.sendLock.Release();
        }
    }

    /// <summary>
    /// Ends the export, with the notification of its success or without any.
    /// </summary>
    /// <param name="success">The notification of the success, or null when the export failed.</param>
    private async Task FinishAsync(DataFileExportSuccessMessage? success)
    {
        await this.runningMessageCancellation.CancelAsync();
        await this.sendLock.WaitAsync();
        try
        {
            if (this.isFinished)
                return;

            this.isFinished = true;
            if (success is { } successMessage)
                await this.send(Event.SHOW_FILE_EXPORT_SUCCESS, successMessage);
            else if (this.isRunningMessageShown)
                await this.send(Event.HIDE_FILE_EXPORT_RUNNING, this.runningMessage);
        }
        finally
        {
            this.sendLock.Release();
        }
    }

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        await this.FinishAsync(null);

        // The delayed notification may still wait for the lock; it sees the end and gives up:
        await this.delayedRunningMessage;

        this.runningMessageCancellation.Dispose();
        this.sendLock.Dispose();
    }

    #endregion
}