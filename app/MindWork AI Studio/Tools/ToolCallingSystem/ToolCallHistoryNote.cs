using System.Buffers;
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

    /// <summary>
    /// What a note shows for an argument value the tool marked as sensitive, see
    /// IToolImplementation.SensitiveTraceArgumentNames, or which the trace did not keep.
    /// </summary>
    /// <remarks>
    /// Not the mask the user sees in the trace: the model would pass it back word for word and, e.g.,
    /// search for five asterisks.
    /// </remarks>
    public const string HIDDEN_VALUE = "(hidden by AI Studio)";

    /// <summary>
    /// What a note puts behind an argument value it cut at MAX_ARGUMENT_CHARACTERS.
    /// </summary>
    public const string SHORTENED_MARK = "… (shortened by AI Studio)";

    /// <summary>
    /// What the system prompt tells the model about the notes, once a conversation holds one.
    /// </summary>
    /// <remarks>
    /// Without it, a model may take a note for a part of its own answer, or may conclude that it still
    /// has the content. It holds no web address, since every address of the system prompt counts as
    /// given to the model, see ChatThread.IsWebAddressGivenToTheModel.
    /// </remarks>
    public const string SYSTEM_PROMPT_EXPLANATION = """
        # Notes from AI Studio in your earlier answers
        Some of your earlier answers start with a note between <ai_studio_history_note> tags. AI Studio wrote it, not you: it names the tools you called and the sources you used for that answer, and when you wrote it. The results of those tool calls are not kept. When a question needs content you fetched earlier, call the tool again with the same arguments if the tool is available, instead of answering from memory. A value shown as (hidden by AI Studio) or ending in (shortened by AI Studio) is not the one you passed: work it out again from the conversation before you call the tool, and never pass the marker itself. When the tool is not available, tell the user that you read it earlier but no longer have its content. Never write such a note yourself.
        """;

    /// <summary>
    /// Whether any earlier answer of a conversation goes to the model with a note.
    /// </summary>
    /// <param name="blocks">The blocks of the conversation.</param>
    /// <returns>True when at least one answer gets a note.</returns>
    public static bool IsInAnyOf(IEnumerable<ContentBlock> blocks) => blocks.Any(block =>
        block is { Role: ChatRole.AI, ContentType: ContentType.TEXT, Content: ContentText { IsStreaming: false } answer } &&
        !string.IsNullOrWhiteSpace(answer.Text) &&
        Build(answer, block.Time) is not null);

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
        note.AppendLineLF(START_TAG);
        note.AppendLineLF(string.Create(CultureInfo.InvariantCulture, $"Note from AI Studio, not part of your answer. For the answer below, written {time.UtcDateTime:yyyy-MM-dd HH:mm} UTC, you used:"));

        if (calls.Count > 0)
        {
            note.AppendLineLF("Tool calls (their results are not kept; call the tool again if you need the content):");
            foreach (var call in calls)
                note.AppendLineLF($"- {call}");

            if (furtherCalls > 0)
                note.AppendLineLF(string.Create(CultureInfo.InvariantCulture, $"- … ({furtherCalls} more)"));
        }

        if (sources.Count > 0)
        {
            note.AppendLineLF("Sources:");
            foreach (var source in sources)
                note.AppendLineLF($"- {source}");

            if (furtherSources > 0)
                note.AppendLineLF(string.Create(CultureInfo.InvariantCulture, $"- … ({furtherSources} more)"));
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
                ToolInvocationTraceStatus.SUCCESS when trace.WasExecuted => $"{trace.ToolId} {WriteArguments(trace)}",
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
    /// Read from ToolInvocationTrace.ArgumentsJson, which keeps every value with its JSON type, rather
    /// than from the texts the user reads, which do not. An argument the trace names without keeping
    /// its value was sensitive and stays hidden; so do all values of a trace from before ArgumentsJson
    /// existed. A long text is cut, also inside an array or an object, so that every value keeps its
    /// type. Both are marked, see HIDDEN_VALUE and SHORTENED_MARK, so the model does not pass them back
    /// as they stand.
    /// </remarks>
    private static string WriteArguments(ToolInvocationTrace trace)
    {
        using var keptArguments = ReadKeptArguments(trace.ArgumentsJson);
        var keptValues = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (keptArguments is not null)
        {
            foreach (var property in keptArguments.RootElement.EnumerateObject())
                keptValues.TryAdd(property.Name, property.Value);
        }

        var names = new SortedSet<string>(trace.Arguments.Keys, StringComparer.Ordinal);
        names.UnionWith(keptValues.Keys);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, ARGUMENT_WRITER_OPTIONS))
        {
            writer.WriteStartObject();
            foreach (var name in names)
            {
                writer.WritePropertyName(name);
                if (keptValues.TryGetValue(name, out var value))
                    WriteValue(writer, value);
                else
                    writer.WriteStringValue(HIDDEN_VALUE);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// The arguments a trace kept with their values, or null when it kept none.
    /// </summary>
    /// <remarks>
    /// AI Studio writes them itself, see ToolExecutor. Text which is no JSON object, as in a chat file
    /// changed by hand, counts as nothing kept, so its arguments stay hidden.
    /// </remarks>
    private static JsonDocument? ReadKeptArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return null;

        try
        {
            var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind is JsonValueKind.Object)
                return document;

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString() ?? string.Empty;
                writer.WriteStringValue(text.Length > MAX_ARGUMENT_CHARACTERS ? $"{text[..MAX_ARGUMENT_CHARACTERS]}{SHORTENED_MARK}" : text);
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteValue(writer, item);

                writer.WriteEndArray();
                break;

            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            default:
                value.WriteTo(writer);
                break;
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