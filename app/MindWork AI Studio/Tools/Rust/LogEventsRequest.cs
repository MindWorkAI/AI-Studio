namespace AIStudio.Tools.Rust;

/// <summary>
/// A batch of log events which the Rust runtime writes to the log file.
/// </summary>
/// <param name="Events">The events, in the order they happened.</param>
public readonly record struct LogEventsRequest(IReadOnlyList<LogEventRequest> Events);