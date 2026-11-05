using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

using AIStudio.Chat;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The note telling the model which tools and sources an earlier answer of it rested on.
/// </summary>
/// <remarks>
/// The results of the tools do not travel into later messages, see ToolInvocationTrace.Result. Without
/// this note a model would see only the answers it wrote, believe it never read a web page, and refuse
/// to read it again. The note names what was fetched, never its content: the model calls the tool again
/// when it needs the content, which costs nothing while it does not.<br/><br/>
///
/// The note stands in front of the answer in the request only, never in ContentText.Text, so it does not
/// reach a copy, an export, or a text sent on. It never goes into the system prompt either: the addresses
/// there count as given to the model, see ChatThread.IsWebAddressGivenToTheModel, and the note holds
/// addresses the model chose itself.<br/><br/>
///
/// Written in English and not translated, because only the model reads it.
/// </remarks>
public static partial class ToolCallHistoryNote
{
    public const string START_TAG = "<ai_studio_history_note>";

    public const string END_TAG = "</ai_studio_history_note>";

    /// <summary>
    /// The most tool calls one note names.
    /// </summary>
    public const int MAX_CALLS = 20;

    /// <summary>
    /// The most sources one note names.
    /// </summary>
    public const int MAX_SOURCES = 20;

    /// <summary>
    /// The most characters of one argument value a note shows.
    /// </summary>
    public const int MAX_ARGUMENT_CHARACTERS = 300;

    private static readonly JsonWriterOptions ARGUMENT_WRITER_OPTIONS = new()
    {
        // Only the model reads the note, never a browser, so umlauts and the like may stay as they are:
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Builds the note for one answer of the model.
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <param name="time">When the answer was written.</param>
    /// <returns>The note, or null when the answer used neither a tool nor a source.</returns>
    public static string? Build(ContentText answer, DateTimeOffset time)
    {
        var calls = DescribeCalls(answer.ToolInvocations, out var callArgumentTexts, out var furtherCalls);
        var sources = DescribeSources(answer.Sources, callArgumentTexts, out var furtherSources);
        if (calls.Count is 0 && sources.Count is 0)
            return null;

        var note = new StringBuilder();
        note.AppendLine(START_TAG);
        note.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Note from AI Studio, not part of your answer. For the answer below, written {time.UtcDateTime:yyyy-MM-dd HH:mm} UTC, you used:"));

        if (calls.Count > 0)
        {
            note.AppendLine("Tool calls (their results are not kept; call the tool again if you need the content):");
            foreach (var call in calls)
                note.AppendLine($"- {call}");

            if (furtherCalls > 0)
                note.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- … ({furtherCalls} more)"));
        }

        if (sources.Count > 0)
        {
            note.AppendLine("Sources:");
            foreach (var source in sources)
                note.AppendLine($"- {source}");

            if (furtherSources > 0)
                note.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- … ({furtherSources} more)"));
        }

        note.Append(END_TAG);
        return note.ToString();
    }

    /// <summary>
    /// Removes every note a model wrote into its answer itself.
    /// </summary>
    /// <remarks>
    /// A model which sees the note in front of its earlier answers may start writing one of its own.
    /// Sent back, such a note would claim tool calls which never happened.
    /// </remarks>
    /// <param name="text">The text of an answer.</param>
    /// <returns>The text without notes.</returns>
    public static string RemoveNotesWrittenByModel(string text) => text.Contains(START_TAG, StringComparison.OrdinalIgnoreCase)
        ? NotePattern().Replace(text, string.Empty)
        : text;

    [GeneratedRegex(@"<ai_studio_history_note>.*?(</ai_studio_history_note>|\z)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NotePattern();

    /// <summary>
    /// One line per tool call: the calls which ran with their arguments, the others with their outcome only.
    /// </summary>
    /// <remarks>
    /// A call which failed or was blocked is named, so the model does not repeat it blindly, but without
    /// its arguments and its message: the message may quote what went wrong, content included.
    /// </remarks>
    private static List<string> DescribeCalls(IEnumerable<ToolInvocationTrace> traces, out List<string> argumentTexts, out int furtherCalls)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        argumentTexts = [];
        furtherCalls = 0;

        foreach (var trace in traces.OrderBy(trace => trace.Order))
        {
            // An invalid call has no tool to name:
            if (string.IsNullOrWhiteSpace(trace.ToolId))
                continue;

            var line = trace.Status switch
            {
                ToolInvocationTraceStatus.SUCCESS when trace.WasExecuted => $"{trace.ToolId} {WriteArguments(trace.Arguments)}",
                ToolInvocationTraceStatus.BLOCKED => $"{trace.ToolId} -> blocked",
                ToolInvocationTraceStatus.ERROR => $"{trace.ToolId} -> failed",
                _ => null,
            };

            if (line is null || !seen.Add(line))
                continue;

            if (lines.Count >= MAX_CALLS)
            {
                furtherCalls++;
                continue;
            }

            lines.Add(line);
            if (trace.Status is ToolInvocationTraceStatus.SUCCESS)
                argumentTexts.AddRange(trace.Arguments.Values);
        }

        return lines;
    }

    /// <summary>
    /// The arguments of a call as one JSON object, so the model can pass them again as they are.
    /// </summary>
    /// <remarks>
    /// The trace keeps every value as text, see ToolExecutor.FormatArguments. A value which reads as a
    /// JSON number, boolean, array, or object was one, and goes back as such; everything else is a string.
    /// A value the tool marked as sensitive stays masked.
    /// </remarks>
    private static string WriteArguments(Dictionary<string, string> arguments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, ARGUMENT_WRITER_OPTIONS))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in arguments.OrderBy(argument => argument.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(name);
                if (value.Length > MAX_ARGUMENT_CHARACTERS)
                    writer.WriteStringValue($"{value[..MAX_ARGUMENT_CHARACTERS]}…");
                else if (IsJsonValueOtherThanString(value))
                    writer.WriteRawValue(value);
                else
                    writer.WriteStringValue(value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool IsJsonValueOtherThanString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null or JsonValueKind.Undefined);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// One line per source: where it came from and its address.
    /// </summary>
    /// <remarks>
    /// Without the title: a title is text of a foreign page, which reached the model through the
    /// prompt injection filter once, and would reach it unfiltered with every later message. A source
    /// whose address the model passed to a tool already stands with that call.
    /// </remarks>
    private static List<string> DescribeSources(IEnumerable<Source> sources, List<string> callArgumentTexts, out int furtherSources)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        furtherSources = 0;

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.URL) || !seen.Add(source.URL))
                continue;

            if (callArgumentTexts.Any(argument => argument.Equals(source.URL, StringComparison.Ordinal)))
                continue;

            if (lines.Count >= MAX_SOURCES)
            {
                furtherSources++;
                continue;
            }

            var origin = source.Origin switch
            {
                SourceOrigin.TOOL => "tool",
                SourceOrigin.RAG => "data source",
                SourceOrigin.LLM => "search of the provider",
                _ => "unknown",
            };

            lines.Add($"[{origin}] {source.URL}");
        }

        return lines;
    }
}