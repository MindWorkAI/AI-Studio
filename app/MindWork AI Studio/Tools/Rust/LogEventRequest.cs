namespace AIStudio.Tools.Rust;

/// <summary>
/// A log event the Rust runtime writes to the log file.
/// </summary>
/// <param name="Timestamp">When the event happened, in the round-trip format of TerminalLogger.FormatTransportTimestamp, e.g., 2026-10-09T17:53:54.5629130+00:00. The Rust runtime logs the event with this time; when it cannot read it, the event shows the time it arrived instead.</param>
/// <param name="Level">The log level.</param>
/// <param name="Category">The category of the log event.</param>
/// <param name="Message">The log message.</param>
/// <param name="Exception">Optional exception message.</param>
/// <param name="StackTrace">Optional exception stack trace.</param>
public readonly record struct LogEventRequest(
    string Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? StackTrace
);
