using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Reads a streamed Anthropic messages call back into the answer the tool calling loop works with.
/// </summary>
/// <remarks>
/// Anthropic streams a message as a set of content blocks which open, grow, and close, correlated
/// by their index and interleaved with one another. This type keeps one builder per index and
/// hands out text as it arrives; everything else is bookkeeping until the message ends.<br/><br/>
/// No HTTP, no dependency injection, no provider: what happens here are decisions about bytes,
/// and those are the decisions worth having a test for.
/// </remarks>
public sealed class AnthropicMessageStreamAccumulator
{
    private const string EVENT_MESSAGE_START = "message_start";
    private const string EVENT_BLOCK_START = "content_block_start";
    private const string EVENT_BLOCK_DELTA = "content_block_delta";
    private const string EVENT_BLOCK_STOP = "content_block_stop";
    private const string EVENT_MESSAGE_DELTA = "message_delta";
    private const string EVENT_MESSAGE_STOP = "message_stop";
    
    private const string DELTA_TEXT = "text_delta";
    
    private readonly Dictionary<int, AnthropicContentBlockBuilder> openBlocks = [];
    private readonly SortedDictionary<int, JsonElement> finishedBlocks = [];
    private readonly Dictionary<string, string> unparsableToolArguments = [];
    private string stopReason = string.Empty;
    private bool messageEnded;
    
    /// <summary>
    /// Takes the next event of the stream and returns what it has to show.
    /// </summary>
    /// <param name="serverSentEvent">The event to read.</param>
    /// <returns>The text of this event, empty when it carried none, and the usage of the message start.</returns>
    public AnthropicStreamPart Process(ServerSentEvent serverSentEvent)
    {
        if (serverSentEvent.Data.Length is 0)
            return AnthropicStreamPart.Nothing;

        AnthropicStreamLine line;
        try
        {
            line = JsonSerializer.Deserialize<AnthropicStreamLine>(serverSentEvent.Data, ProviderJsonOptions.OPTIONS);
        }
        catch (JsonException)
        {
            // A line we cannot read is a line we skip, exactly as the plain text path does:
            return AnthropicStreamPart.Nothing;
        }

        switch (line.Type)
        {
            case EVENT_MESSAGE_START:
                //
                // What the request carried, read the same way as on the plain text path and for
                // the same reason: the start states this request alone, cf. ResponseStreamLine.
                //
                var usage = line.Message?.Usage?.ToTokenUsage() ?? TokenUsage.UNKNOWN;
                return usage.IsKnown ? new AnthropicStreamPart(string.Empty, usage) : AnthropicStreamPart.Nothing;

            case EVENT_BLOCK_START:
                this.openBlocks[line.Index] = new AnthropicContentBlockBuilder(line.ContentBlock);
                return AnthropicStreamPart.Nothing;
            
            case EVENT_BLOCK_DELTA:
                if (!this.openBlocks.TryGetValue(line.Index, out var openBlock))
                {
                    //
                    // A delta for a block which never opened. Only text can be salvaged from
                    // that: a tool use without its ID and name is unanswerable, and thinking
                    // without its signature would have the next round refused. Text is kept as a
                    // block of its own so that what the user reads is what the model is told it
                    // said.
                    //
                    if (line.Delta.Type is not DELTA_TEXT)
                        return AnthropicStreamPart.Nothing;

                    openBlock = new AnthropicContentBlockBuilder(EmptyTextBlock());
                    this.openBlocks[line.Index] = openBlock;
                }

                return new AnthropicStreamPart(openBlock.Append(line.Delta));
            
            case EVENT_BLOCK_STOP:
                if (this.openBlocks.Remove(line.Index, out var finishedBlock))
                    this.Finish(line.Index, finishedBlock);
                
                return AnthropicStreamPart.Nothing;
            
            case EVENT_MESSAGE_DELTA:
                //
                // The stop reason ends the message as surely as the closing event does. Taking
                // both means a gateway which sends only one of them still gets a round out.
                //
                if (!string.IsNullOrWhiteSpace(line.Delta.StopReason))
                {
                    this.stopReason = line.Delta.StopReason;
                    this.messageEnded = true;
                }
                
                return AnthropicStreamPart.Nothing;
            
            case EVENT_MESSAGE_STOP:
                this.messageEnded = true;
                this.MaterializeOpenBlocks();
                return AnthropicStreamPart.Nothing;
            
            default:
                return AnthropicStreamPart.Nothing;
        }
    }
    
    /// <summary>
    /// Builds the answer of the round from everything the stream said.
    /// </summary>
    /// <returns>
    /// The answer, or null when the stream ended before the message did. Null is how a failed
    /// request and a stream cut off mid-sentence look from here, and both end the round.
    /// </returns>
    public AnthropicResponse? Build()
    {
        if (!this.messageEnded)
            return null;

        // Blocks whose closing event never came are finished here rather than dropped:
        this.MaterializeOpenBlocks();
        
        return new AnthropicResponse
        {
            StopReason = this.stopReason,
            Content = [..this.finishedBlocks.Values],
            UnparsableToolInputs = this.unparsableToolArguments,
        };
    }
    
    private void MaterializeOpenBlocks()
    {
        foreach (var (index, builder) in this.openBlocks)
            this.Finish(index, builder);
        
        this.openBlocks.Clear();
    }
    
    private void Finish(int index, AnthropicContentBlockBuilder builder)
    {
        this.finishedBlocks[index] = builder.Build();
        
        // Read after the block was built, because that is when the arguments are parsed:
        if (builder.UnparsableToolArguments is not null && !string.IsNullOrWhiteSpace(builder.ToolUseId))
            this.unparsableToolArguments[builder.ToolUseId] = builder.UnparsableToolArguments;
    }
    
    private static JsonElement EmptyTextBlock() => JsonSerializer.SerializeToElement(new
    {
        type = "text",
        text = string.Empty,
    });
}