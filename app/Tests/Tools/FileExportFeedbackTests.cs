using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which notifications a file export sends, and in which order.
/// </summary>
/// <remarks>
/// A fast export shows its success right away; a slow one first shows that it runs. That notification
/// waits for the user and must therefore never come after the end of the export: it would stay until
/// the user closes it, claiming an export which is long over.
/// </remarks>
[TestFixture]
public sealed class FileExportFeedbackTests
{
    private const string FILE_PATH = "/home/user/Documents/report.docx";
    private const string ICON = "icon";
    private const string MESSAGE = "The export succeeded.";

    [Test]
    public async Task AFastExportShowsNoRunningNotification()
    {
        var sent = new SentNotifications();
        await using (var feedback = new FileExportFeedback(sent.Send, token => Task.Delay(Timeout.Infinite, token)))
        {
            feedback.ShowRunning(FILE_PATH);
            await feedback.SucceededAsync(ICON, MESSAGE, FILE_PATH, true);
        }

        Assert.That(sent.Events, Is.EqualTo(new[] { Event.SHOW_FILE_EXPORT_SUCCESS }));
    }

    [Test]
    public async Task ASlowExportShowsThatItRunsAndThenItsSuccessUnderTheSameKey()
    {
        var sent = new SentNotifications();
        await using (var feedback = new FileExportFeedback(sent.Send, _ => Task.CompletedTask))
        {
            feedback.ShowRunning(FILE_PATH);
            await sent.RunningShown;
            await feedback.SucceededAsync(ICON, MESSAGE, FILE_PATH, true);
        }

        Assert.That(sent.Events, Is.EqualTo(new[] { Event.SHOW_FILE_EXPORT_RUNNING, Event.SHOW_FILE_EXPORT_SUCCESS }));
        var running = (DataFileExportRunningMessage)sent.Data[0];
        var success = (DataFileExportSuccessMessage)sent.Data[1];
        Assert.That(success.Key, Is.EqualTo(running.Key), "The success has to replace the notification that the export runs.");
    }

    [Test]
    public async Task AFailedExportRemovesItsRunningNotification()
    {
        var sent = new SentNotifications();
        await using (var feedback = new FileExportFeedback(sent.Send, _ => Task.CompletedTask))
        {
            feedback.ShowRunning(FILE_PATH);
            await sent.RunningShown;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Events, Is.EqualTo(new[] { Event.SHOW_FILE_EXPORT_RUNNING, Event.HIDE_FILE_EXPORT_RUNNING }));
            Assert.That(((DataFileExportRunningMessage)sent.Data[1]).Key, Is.EqualTo(((DataFileExportRunningMessage)sent.Data[0]).Key));
        }
    }

    [Test]
    public async Task AnExceptionRemovesTheRunningNotification()
    {
        var sent = new SentNotifications();
        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await using var feedback = new FileExportFeedback(sent.Send, _ => Task.CompletedTask);
            feedback.ShowRunning(FILE_PATH);
            await sent.RunningShown;
            throw new IOException("The target file is locked.");
        });

        Assert.That(sent.Events, Is.EqualTo(new[] { Event.SHOW_FILE_EXPORT_RUNNING, Event.HIDE_FILE_EXPORT_RUNNING }));
    }

    [Test]
    public async Task AFastFailedExportSendsNothing()
    {
        var sent = new SentNotifications();
        await using (var feedback = new FileExportFeedback(sent.Send, token => Task.Delay(Timeout.Infinite, token)))
            feedback.ShowRunning(FILE_PATH);

        Assert.That(sent.Events, Is.Empty, "The error message of the caller says everything.");
    }

    [Test]
    public async Task TheRunningNotificationNeverComesAfterTheEnd()
    {
        //
        // The notification that the export runs becomes due while the end of the export is being sent.
        // The wait ignores its cancellation, as a timer would which fires at that very moment:
        //
        var due = new TaskCompletionSource();
        var sendingSuccess = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSuccess = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new SentNotifications(async () =>
        {
            sendingSuccess.SetResult();
            await releaseSuccess.Task;
        });

        await using (var feedback = new FileExportFeedback(sent.Send, _ => due.Task))
        {
            feedback.ShowRunning(FILE_PATH);
            var ending = feedback.SucceededAsync(ICON, MESSAGE, FILE_PATH, true);

            await sendingSuccess.Task;
            due.SetResult();
            releaseSuccess.SetResult();
            await ending;
        }

        // Disposing waits for the delayed notification, so whatever it would send is in by now:
        Assert.That(sent.Events, Is.EqualTo(new[] { Event.SHOW_FILE_EXPORT_SUCCESS }));
    }

    /// <summary>
    /// Records what a FileExportFeedback sends, in place of the message bus.
    /// </summary>
    /// <param name="whileSendingSuccess">Runs while the success is being sent, before it counts as sent.</param>
    private sealed class SentNotifications(Func<Task>? whileSendingSuccess = null)
    {
        private readonly TaskCompletionSource runningShown = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Event> Events { get; } = [];

        public List<object> Data { get; } = [];

        public Task RunningShown => this.runningShown.Task;

        public async Task Send(Event triggeredEvent, object data)
        {
            if (triggeredEvent is Event.SHOW_FILE_EXPORT_SUCCESS && whileSendingSuccess is not null)
                await whileSendingSuccess();

            this.Events.Add(triggeredEvent);
            this.Data.Add(data);
            if (triggeredEvent is Event.SHOW_FILE_EXPORT_RUNNING)
                this.runningShown.TrySetResult();
        }
    }
}