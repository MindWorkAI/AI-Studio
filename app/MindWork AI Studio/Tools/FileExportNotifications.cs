namespace AIStudio.Tools;

/// <summary>
/// Shows the notifications of file exports in one window, and keeps a late notification that an export
/// runs from appearing after its end.
/// </summary>
/// <remarks>
/// FileExportFeedback sends its notifications in the right order, but they may take effect in another
/// one. The notification that an export runs comes late, from a timer, and waits in the queue of the
/// renderer. The end of an export usually comes from a component, on the thread of the renderer, and
/// takes effect right away: it overtakes the waiting notification, which would then appear after the
/// end and stay until the user closes it. So we remember which exports ended and drop what comes late.
/// <br/><br/>
/// One key stays per export which ended, for as long as the window lives. That is a few bytes per
/// file the user saved, which is not worth a cleanup that could drop a key too early.
/// </remarks>
/// <param name="snackbar">The notifications of the window.</param>
public sealed class FileExportNotifications(ISnackbar snackbar)
{
    private readonly HashSet<string> finishedExports = [];

    public void ShowRunning(DataFileExportRunningMessage message)
    {
        if (this.finishedExports.Contains(message.Key))
            return;

        message.Show(snackbar);
    }

    /// <param name="message">The notification of the success.</param>
    /// <param name="closesAutomatically">Whether the notification closes after a few seconds, as the user chose.</param>
    public void ShowSuccess(DataFileExportSuccessMessage message, bool closesAutomatically)
    {
        this.finishedExports.Add(message.Key);
        message.Show(snackbar, closesAutomatically);
    }

    public void HideRunning(DataFileExportRunningMessage message)
    {
        this.finishedExports.Add(message.Key);
        message.Hide(snackbar);
    }
}