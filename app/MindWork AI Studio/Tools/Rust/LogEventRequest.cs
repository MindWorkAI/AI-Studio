namespace AIStudio.Tools.Rust;

public readonly record struct LogEventRequest(
    string Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    string? StackTrace
);
