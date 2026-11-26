using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Get the paths of the log files.
    /// </summary>
    /// <returns>The paths of the log files.</returns>
    public async Task<GetLogPathsResponse> GetLogPaths()
    {
        return await this.http.GetFromJsonAsync<GetLogPathsResponse>("/log/paths", JSON_OPTIONS);
    }

    /// <summary>
    /// Queues a log event for the Rust runtime.
    /// </summary>
    /// <param name="timestamp">When the log event happened, formatted by TerminalLogger.FormatTransportTimestamp.</param>
    /// <param name="level">The log level.</param>
    /// <param name="category">The category of the log event.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception message.</param>
    /// <param name="stackTrace">Optional exception stack trace.</param>
    /// <remarks>
    /// Never waits: the event joins the queue of RustLogForwarder, which sends it in a batch.
    /// </remarks>
    public void LogEvent(string timestamp, string level, string category, string message, string? exception = null, string? stackTrace = null)
    {
        this.logForwarder.Enqueue(new LogEventRequest(timestamp, level, category, message, exception, stackTrace));
    }
}