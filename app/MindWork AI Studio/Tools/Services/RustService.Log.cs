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
        return await this.http.GetFromJsonAsync<GetLogPathsResponse>("/log/paths", this.jsonRustSerializerOptions);
    }

    /// <summary>
    /// Sends a log event to the Rust runtime.
    /// </summary>
    /// <param name="timestamp">The timestamp of the log event.</param>
    /// <param name="level">The log level.</param>
    /// <param name="category">The category of the log event.</param>
    /// <param name="message">The log message.</param>
    /// <param name="exception">Optional exception message.</param>
    /// <param name="stackTrace">Optional exception stack trace.</param>
    public void LogEvent(string timestamp, string level, string category, string message, string? exception = null, string? stackTrace = null)
    {
        try
        {
            //
            // Fire-and-forget the log event to avoid blocking. This is the one place which deliberately
            // discards its task instead of observing it: observing means logging the failure, and logging
            // means sending another log event through this very method. A broken connection to Rust would
            // feed itself. The unobserved task exception handler in Program.cs remains the safety net here.
            //
            var request = new LogEventRequest(timestamp, level, category, message, exception, stackTrace);
            _ = this.http.PostAsJsonAsync("/log/event", request, this.jsonRustSerializerOptions);
        }
        catch
        {
            //
            // We don't expect this to ever happen because the HTTP client cannot raise exceptions in fire-and-forget mode.
            // This is because we don't await the task, so any exceptions thrown during the HTTP request are not propagated
            // back to the caller.
            //
            
            Console.WriteLine("Failed to send log event to Rust service.");
            // Ignore errors to avoid log loops
        }
    }
}