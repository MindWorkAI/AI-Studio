using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AIStudio.Tools;

public sealed class TerminalLogger() : ConsoleFormatter(FORMATTER_NAME)
{
    public const string FORMATTER_NAME = "AI Studio Terminal Logger";

    private static RustService? RUST_SERVICE;

    /// <summary>
    /// Sets the Rust service for logging events.
    /// </summary>
    /// <param name="service">The Rust service instance.</param>
    public static void SetRustService(RustService service)
    {
        RUST_SERVICE = service;
    }
    
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLevel = logEntry.LogLevel.ToString();
        var category = logEntry.Category;
        var exception = logEntry.Exception?.ToString();

        textWriter.Write($"[{timestamp}] {logLevel} [{category}] {message}");
        if (exception is not null)
            textWriter.Write($" Exception: {exception}");

        textWriter.WriteLine();

        // Send log event to Rust via API (fire-and-forget):
        RUST_SERVICE?.LogEvent(timestamp, logLevel, category, message, exception);
    }
}