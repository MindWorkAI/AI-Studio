using System.Collections.Concurrent;
using System.Globalization;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AIStudio.Tools;

public sealed class TerminalLogger() : ConsoleFormatter(FORMATTER_NAME)
{
    public const string FORMATTER_NAME = "AI Studio Terminal Logger";

    private static RustService? RUST_SERVICE;
    
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    // ReSharper disable ConvertToConstant.Local
    private static bool LOG_TO_STDOUT = true;
    // ReSharper restore ConvertToConstant.Local
    // ReSharper restore FieldCanBeMadeReadOnly.Local

    // Buffer for early log events before the RustService is available:
    private static readonly ConcurrentQueue<LogEventRequest> EARLY_LOG_BUFFER = new();

    // ANSI color codes for log levels:
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

        // Flush all buffered early log events to Rust in the original order:
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

        #if !DEBUG
        LOG_TO_STDOUT = false;
        #endif
    }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var transportTimestamp = FormatTransportTimestamp(now);
        var logLevel = logEntry.LogLevel.ToString();
        var category = logEntry.Category;
        var exceptionMessage = logEntry.Exception?.Message;
        var stackTrace = logEntry.Exception?.StackTrace;
        var colorCode = GetColorForLogLevel(logEntry.LogLevel);

        if (LOG_TO_STDOUT)
        {
            textWriter.Write($"[{colorCode}{timestamp}{ANSI_RESET}] {colorCode}{logLevel}{ANSI_RESET} [{category}] {colorCode}{message}{ANSI_RESET}");
            if (logEntry.Exception is not null)
            {
                textWriter.Write($"   {colorCode}Exception: {exceptionMessage}{ANSI_RESET}");
                if (stackTrace is not null)
                {
                    textWriter.WriteLine();
                    foreach (var line in stackTrace.Split('\n'))
                        textWriter.WriteLine($"      {colorCode}{line.TrimEnd()}{ANSI_RESET}");
                }
            }
            else
                textWriter.WriteLine();
        }

        // Send log event to Rust via API (fire-and-forget):
        if (RUST_SERVICE is not null)
            RUST_SERVICE.LogEvent(transportTimestamp, logLevel, category, message, exceptionMessage, stackTrace);
        
        // Buffer early log events until the RustService is available:
        else
            EARLY_LOG_BUFFER.Enqueue(new LogEventRequest(transportTimestamp, logLevel, category, message, exceptionMessage, stackTrace));
    }

    /// <summary>
    /// Formats the time of a log event for the Rust runtime.
    /// </summary>
    /// <remarks>
    /// The round-trip format is ISO 8601 with seven fractional digits and the offset, e.g.,
    /// 2026-10-09T17:53:54.5629130+00:00, and it is the same in every culture. The Rust runtime
    /// reads it as RFC 3339, so it can log the event with the time it happened instead of the
    /// time it arrived.
    /// </remarks>
    /// <param name="timestamp">The time of the log event.</param>
    /// <returns>The time in the round-trip format.</returns>
    public static string FormatTransportTimestamp(DateTimeOffset timestamp) => timestamp.ToString("O", CultureInfo.InvariantCulture);

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
