using System.Text;
using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Reads a streamed Chat Completions answer back into the message the tool calling loop works with.
/// </summary>
/// <remarks>
/// This one path serves seventeen providers, which is why every correlation here is staggered
/// rather than assumed: a call is found by its index, failing that by its ID, failing that it is
/// the one most recently opened. Gateways differ in all of these, and in whether they close the
/// stream with a "[DONE]" at all.<br/><br/>
/// No HTTP, no dependency injection, no provider: what happens here are decisions about bytes,
/// and those are the decisions worth having a test for.
/// </remarks>
/// <param name="readSources">
/// Reads the sources out of one line, in whichever shape this provider sends them. Left out, the
/// round runs without sources, which is what a provider that sends none needs.
/// </param>
public sealed class ChatCompletionToolCallAccumulator(Func<ServerSentEvent, IList<ISource>>? readSources = null)
{
    private const string DONE = "[DONE]";
    private const string EMPTY_ARGUMENTS = "{}";
    
    private readonly StringBuilder text = new();
    private readonly StringBuilder reasoning = new();
    private readonly List<ToolCallBuilder> toolCalls = [];
    private readonly Dictionary<int, ToolCallBuilder> toolCallsByIndex = [];
    private bool hasReadAnything;
    
    /// <summary>
    /// Takes the next event of the stream and returns what it has to show.
    /// </summary>
    /// <param name="serverSentEvent">The event to read.</param>
    /// <returns>The text of this event, empty when it carried none.</returns>
    public ChatCompletionStreamPart Process(ServerSentEvent serverSentEvent)
    {
        if (serverSentEvent.Data.Length is 0 || serverSentEvent.Data is DONE)
            return ChatCompletionStreamPart.Nothing;

        ChatCompletionToolStreamLine? line;
        try
        {
            line = JsonSerializer.Deserialize<ChatCompletionToolStreamLine>(serverSentEvent.Data, ProviderJsonOptions.OPTIONS);
        }
        catch (JsonException)
        {
            // A line we cannot read is a line we skip, exactly as the plain text path does:
            return ChatCompletionStreamPart.Nothing;
        }

        //
        // Only the first choice is ever used, here as much as on the plain text path: we never
        // ask for more than one, and a provider which sends more has no say in which one counts.
        //
        //
        // Sources are read off the same line, through the provider's own types: they may sit on
        // a line of their own or right next to the text, and a line without any gives an empty
        // list either way.
        //
        var sources = readSources?.Invoke(serverSentEvent) ?? [];
        
        var delta = line?.Choices?.FirstOrDefault()?.Delta;
        if (delta is null)
            return WithSources(string.Empty, sources);

        this.hasReadAnything = true;
        
        if (!string.IsNullOrEmpty(delta.ReasoningContent))
            this.reasoning.Append(delta.ReasoningContent);

        foreach (var toolCallDelta in delta.ToolCalls ?? [])
        {
            if (toolCallDelta is null)
                continue;
            
            this.Apply(toolCallDelta);
        }

        var textDelta = delta.Content;
        if (textDelta.Length is 0)
            return WithSources(string.Empty, sources);

        this.text.Append(textDelta);
        return new ChatCompletionStreamPart(textDelta, sources);
    }
    
    /// <summary>
    /// Builds the message of the round from everything the stream said.
    /// </summary>
    /// <returns>
    /// The message, or null when no line of the stream was readable at all. Null is how a failed
    /// request looks from here, and it ends the round.
    /// </returns>
    /// <remarks>
    /// The end of the stream is the end of the message. There is nothing else to wait for: a
    /// "[DONE]" is not sent by every gateway, and a finish reason not by every one either.
    /// </remarks>
    public ChatCompletionResponseMessage? Build()
    {
        if (!this.hasReadAnything)
            return null;

        var answer = this.text.ToString();
        return new ChatCompletionResponseMessage
        {
            Role = "assistant",
            
            //
            // No text means no content field, the way a round which only calls a tool arrives
            // when it is not streamed. Some providers reject an empty string in its place.
            //
            RawContent = answer.Length is 0 ? null : JsonSerializer.SerializeToElement(answer),
            ReasoningContent = this.reasoning.Length is 0 ? null : this.reasoning.ToString(),
            ToolCalls = this.toolCalls.Count is 0
                ? null
                : this.toolCalls.Select(toolCall => (ChatCompletionToolCall?)toolCall.Build()).ToList(),
        };
    }
    
    private void Apply(ChatCompletionToolCallDelta toolCallDelta)
    {
        var toolCall = this.Resolve(toolCallDelta);
        
        //
        // The first non-empty value wins for everything but the arguments: some providers repeat
        // the ID and the name with every fragment, and a later empty one must not erase them.
        //
        toolCall.Id ??= Coalesce(toolCallDelta.Id);
        toolCall.Type ??= Coalesce(toolCallDelta.Type);
        toolCall.Name ??= Coalesce(toolCallDelta.Function?.Name);
        
        // The arguments are the one thing that is always appended, because that is how they come:
        if (!string.IsNullOrEmpty(toolCallDelta.Function?.Arguments))
            toolCall.Arguments.Append(toolCallDelta.Function.Arguments);
    }
    
    /// <summary>
    /// Finds the call a fragment belongs to, or opens a new one for it.
    /// </summary>
    private ToolCallBuilder Resolve(ChatCompletionToolCallDelta toolCallDelta)
    {
        //
        // The index is what the specification correlates by, so it comes first:
        //
        if (toolCallDelta.Index is { } index)
        {
            if (this.toolCallsByIndex.TryGetValue(index, out var knownByIndex))
                return knownByIndex;

            var openedByIndex = this.Open();
            this.toolCallsByIndex[index] = openedByIndex;
            return openedByIndex;
        }

        //
        // Some gateways leave the index out and correlate by ID instead:
        //
        if (!string.IsNullOrWhiteSpace(toolCallDelta.Id))
        {
            var knownById = this.toolCalls.FirstOrDefault(x => string.Equals(x.Id, toolCallDelta.Id, StringComparison.Ordinal));
            if (knownById is not null)
                return knownById;

            return this.Open();
        }

        //
        // And some send neither once the call is open, which leaves the one we opened last. A
        // fragment before any call was opened opens one, rather than being dropped.
        //
        return this.toolCalls.Count > 0 ? this.toolCalls[^1] : this.Open();
    }
    
    private ToolCallBuilder Open()
    {
        var toolCall = new ToolCallBuilder();
        this.toolCalls.Add(toolCall);
        return toolCall;
    }
    
    private static string? Coalesce(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    
    /// <summary>
    /// A part for a line which brought sources but no text, or nothing at all.
    /// </summary>
    private static ChatCompletionStreamPart WithSources(string text, IList<ISource> sources)
        => sources.Count is 0 ? ChatCompletionStreamPart.Nothing : new ChatCompletionStreamPart(text, sources);

    /// <summary>
    /// One tool call while its fragments are still arriving.
    /// </summary>
    private sealed class ToolCallBuilder
    {
        public string? Id { get; set; }
        
        public string? Type { get; set; }
        
        public string? Name { get; set; }
        
        public StringBuilder Arguments { get; } = new();

        /// <summary>
        /// Builds the call in the shape a non-streamed answer would have carried it.
        /// </summary>
        /// <remarks>
        /// A call without an ID, without a name, or with arguments which are not an object stays
        /// as it is: the adapter has to see what the model actually sent, so that it can reject
        /// the call the way an invalid one has to be rejected.<br/><br/>
        /// Empty arguments are the one exception, and they are not a correction but a
        /// translation: a tool which takes nothing gets no fragment at all here, while the same
        /// call arrives as an empty object when it is not streamed. Handing on the empty string
        /// would have every parameterless tool rejected as invalid.
        /// </remarks>
        public ChatCompletionToolCall Build() => new()
        {
            Id = this.Id,
            Type = this.Type ?? "function",
            Function = new ChatCompletionToolFunction
            {
                Name = this.Name,
                Arguments = this.Arguments.Length is 0 ? EMPTY_ARGUMENTS : this.Arguments.ToString(),
            },
        };
    }
}