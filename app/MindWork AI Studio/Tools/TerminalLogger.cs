using System.Collections.Concurrent;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AIStudio.Tools;

public sealed class TerminalLogger() : ConsoleFormatter(FORMATTER_NAME)
{
    public const string FORMATTER_NAME = "AI Studio Terminal Logger";

    private static RustService? RUST_SERVICE;

    // Buffer for early log events before RustService is available
    private static readonly ConcurrentQueue<LogEventRequest> EARLY_LOG_BUFFER = new();

    // ANSI color codes for log levels
    private const string ANSI_RESET = "\x1b[0m";
    private const string ANSI_GRAY = "\x1b[90m";      // Trace, Debug
    private const string ANSI_GREEN = "\x1b[32m";     // Information
    private const string ANSI_YELLOW = "\x1b[33m";    // Warning
    private const string ANSI_RED = "\x1b[91m";       // Error, Critical

    /// <summary>
    /// Sets the Rust service for logging events and flushes any buffered early log events.
    /// </summary>
    /// <param name="service">The Rust service instance.</param>
    public static void SetRustService(RustService service)
    {
        RUST_SERVICE = service;

        // Flush all buffered early log events to Rust in original order
        while (EARLY_LOG_BUFFER.TryDequeue(out var bufferedEvent))
        {
            service.LogEvent(
                bufferedEvent.Timestamp,
                bufferedEvent.Level,
                bufferedEvent.Category,
                bufferedEvent.Message,
                bufferedEvent.Exception,
                bufferedEvent.StackTrace
            );
        }
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLevel = logEntry.LogLevel.ToString();
        var category = logEntry.Category;
        var exceptionMessage = logEntry.Exception?.Message;
        var stackTrace = logEntry.Exception?.StackTrace;
        var colorCode = GetColorForLogLevel(logEntry.LogLevel);

        textWriter.Write($"[{colorCode}{timestamp}{ANSI_RESET}] {colorCode}{logLevel}{ANSI_RESET} [{category}] {colorCode}{message}{ANSI_RESET}");
        if (logEntry.Exception is not null)
        {
            textWriter.Write($"   {colorCode}Exception: {exceptionMessage}{ANSI_RESET}");
            if (stackTrace is not null)
            {
                textWriter.WriteLine();
                foreach (var line in stackTrace.Split('\n'))
                    textWriter.WriteLine($"      {line.TrimEnd()}");
            }
        }
        else
            textWriter.WriteLine();

        // Send log event to Rust via API (fire-and-forget):
        if (RUST_SERVICE is not null)
        {
            RUST_SERVICE.LogEvent(timestamp, logLevel, category, message, exceptionMessage, stackTrace);
        }
        else
        {
            // Buffer early log events until RustService is available
            EARLY_LOG_BUFFER.Enqueue(new LogEventRequest(timestamp, logLevel, category, message, exceptionMessage, stackTrace));
        }
    }

    private static string GetColorForLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => ANSI_GRAY,
        LogLevel.Debug => ANSI_GRAY,
        LogLevel.Information => ANSI_GREEN,
        LogLevel.Warning => ANSI_YELLOW,
        LogLevel.Error => ANSI_RED,
        LogLevel.Critical => ANSI_RED,
        _ => ANSI_RESET
    };
}