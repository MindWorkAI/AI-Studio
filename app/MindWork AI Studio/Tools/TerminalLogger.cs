using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace AIStudio.Tools;

public sealed class TerminalLogger() : ConsoleFormatter(FORMATTER_NAME)
{
    public const string FORMATTER_NAME = "AI Studio Terminal Logger";
    
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLevel = logEntry.LogLevel.ToString();
        var category = logEntry.Category;
        
        textWriter.Write($"=> {timestamp} [{logLevel}] {category}: {message}");
        if (logEntry.Exception is not null)
            textWriter.Write($" Exception was = {logEntry.Exception}");
        
        textWriter.WriteLine();
    }
}