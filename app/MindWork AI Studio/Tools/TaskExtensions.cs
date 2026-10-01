namespace AIStudio.Tools;

public static class TaskExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(TaskExtensions));

    /// <summary>
    /// Lets a task run on its own, but keeps an eye on how it ends.
    /// </summary>
    /// <remarks>
    /// Use this wherever a task is started without awaiting it. Discarding one instead means that nobody
    /// ever looks at its outcome: the task carries its exception until the garbage collector finalizes it,
    /// and only then does it show up as an unobserved task exception — naming a task type, without any
    /// hint at what was running. Around a circuit which is gone, that is the common case: components of a
    /// reloaded or sleeping window still receive events and still schedule work.
    /// </remarks>
    /// <param name="task">The task to watch.</param>
    /// <param name="context">What this task was doing, for the log entry.</param>
    public static void Observe(this Task task, string context)
    {
        task.ContinueWith(finishedTask =>
                LogFailure(finishedTask.Exception, context),
                CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    private static void LogFailure(AggregateException? exception, string context)
    {
        if (exception is null)
            return;

        foreach (var innerException in exception.Flatten().InnerExceptions)
        {
            switch (innerException)
            {
                //
                // The browser connection is gone, or the component was disposed while its work was still
                // on its way. Neither is a defect: it is what a reload or a closed window looks like.
                //
                case JSDisconnectedException:
                case ObjectDisposedException:
                case OperationCanceledException:
                    LOGGER.LogDebug("Background work '{Context}' stopped because its circuit was gone: {Reason}", context, innerException.Message);
                    break;

                default:
                    LOGGER.LogError(innerException, "Background work '{Context}' failed.", context);
                    break;
            }
        }
    }
}