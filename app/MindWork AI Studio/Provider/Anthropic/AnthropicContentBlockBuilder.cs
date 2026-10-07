using System.Buffers;
using System.Text;
using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

/// <summary>
/// Puts one streamed content block back together.
/// </summary>
/// <remarks>
/// A block opens with a seed, grows through fragments, and has to end up as the very block the
/// provider would have sent had we not streamed: it goes back on the next request, and Anthropic
/// checks what it gets. A thinking block is the sharp edge here -- its signature has to return
/// byte for byte with the text it was made for, or the next round is refused with a 400.<br/><br/>
/// This is a pure function over bytes: no HTTP, no state beyond the block itself. That is what
/// makes it the piece worth testing against recorded streams.
/// </remarks>
public sealed class AnthropicContentBlockBuilder
{
    private const string TYPE_TEXT = "text";
    private const string TYPE_TOOL_USE = "tool_use";
    private const string TYPE_THINKING = "thinking";
    
    private const string DELTA_TEXT = "text_delta";
    private const string DELTA_INPUT_JSON = "input_json_delta";
    private const string DELTA_THINKING = "thinking_delta";
    private const string DELTA_SIGNATURE = "signature_delta";
    
    private const string EMPTY_OBJECT = "{}";
    
    private readonly JsonElement seed;
    private readonly StringBuilder text = new();
    private readonly StringBuilder toolArguments = new();
    private readonly StringBuilder thinking = new();
    private string signature;
    
    /// <summary>
    /// Opens a block from the seed the provider sent for it.
    /// </summary>
    /// <param name="contentBlock">The block as it opened.</param>
    public AnthropicContentBlockBuilder(JsonElement contentBlock)
    {
        //
        // The seed is cloned because the document it was read from is gone by the time this block
        // is built, and an element which outlives its document reads memory that is no longer
        // there.
        //
        this.seed = contentBlock.ValueKind is JsonValueKind.Object ? contentBlock.Clone() : default;
        this.BlockType = ReadString(this.seed, "type");
        
        //
        // Anthropic seeds a block with what it already has, which is usually nothing. When it is
        // not nothing, it belongs in front of everything that follows.
        //
        this.text.Append(ReadString(this.seed, TYPE_TEXT));
        this.thinking.Append(ReadString(this.seed, TYPE_THINKING));
        this.signature = ReadString(this.seed, "signature");
    }
    
    /// <summary>
    /// What kind of block this is: text, a tool use, thinking, or something we do not know.
    /// </summary>
    public string BlockType { get; }
    
    /// <summary>
    /// The ID of the tool use, for a tool use block.
    /// </summary>
    public string ToolUseId => ReadString(this.seed, "id");
    
    /// <summary>
    /// The tool arguments as they came off the wire, set only when they never parsed into an object.
    /// </summary>
    /// <remarks>
    /// The block itself carries an empty object then, because that is what may go back to the
    /// provider. The call still has to be rejected rather than run with no arguments at all,
    /// which is what this text is for.
    /// </remarks>
    public string? UnparsableToolArguments { get; private set; }
    
    /// <summary>
    /// Adds the next piece of this block.
    /// </summary>
    /// <param name="delta">The piece as it arrived.</param>
    /// <returns>The text to show, empty for every piece which is not text.</returns>
    public string Append(AnthropicStreamDelta delta)
    {
        switch (delta.Type)
        {
            case DELTA_TEXT when delta.Text is not null:
                this.text.Append(delta.Text);
                return delta.Text;
            
            case DELTA_INPUT_JSON when delta.PartialJson is not null:
                this.toolArguments.Append(delta.PartialJson);
                return string.Empty;
            
            case DELTA_THINKING when delta.Thinking is not null:
                this.thinking.Append(delta.Thinking);
                return string.Empty;
            
            case DELTA_SIGNATURE when delta.Signature is not null:
                this.signature = delta.Signature;
                return string.Empty;
            
            default:
                return string.Empty;
        }
    }
    
    /// <summary>
    /// Builds the finished block, in the shape a non-streamed call would have returned it.
    /// </summary>
    public JsonElement Build()
    {
        switch (this.BlockType)
        {
            case TYPE_TEXT:
                return this.BuildFromSeed(new()
                {
                    ["type"] = JsonSerializer.Serialize(TYPE_TEXT),
                    ["text"] = JsonSerializer.Serialize(this.text.ToString()),
                });
            
            case TYPE_THINKING:
                //
                // The signature travels with the thinking it belongs to. Anthropic refuses the
                // next round without it, so it is written even when it stayed empty: a missing
                // field and an empty one fail the same way, and the empty one says where to look.
                //
                return this.BuildFromSeed(new()
                {
                    ["type"] = JsonSerializer.Serialize(TYPE_THINKING),
                    ["thinking"] = JsonSerializer.Serialize(this.thinking.ToString()),
                    ["signature"] = JsonSerializer.Serialize(this.signature),
                });
            
            case TYPE_TOOL_USE:
                return this.BuildFromSeed(new()
                {
                    ["input"] = this.BuildToolInput(),
                });
            
            default:
                //
                // Redacted thinking and anything we have not seen before go back untouched. We
                // cannot read them, which is precisely why we must not rewrite them either.
                //
                return this.seed;
        }
    }
    
    /// <summary>
    /// The tool arguments as the JSON object they have to be.
    /// </summary>
    /// <remarks>
    /// A tool without arguments gets no fragment at all, so an empty buffer is an empty object.
    /// A buffer which is not an object is kept aside instead: the block needs something the
    /// provider accepts, while the call needs the text that made it invalid.
    /// </remarks>
    private string BuildToolInput()
    {
        var arguments = this.toolArguments.ToString();
        if (string.IsNullOrWhiteSpace(arguments))
            return EMPTY_OBJECT;
        
        try
        {
            using var document = JsonDocument.Parse(arguments);
            if (document.RootElement.ValueKind is JsonValueKind.Object)
                return arguments;
        }
        catch (JsonException)
        {
            // Falls through to the same place a well-formed non-object does:
        }
        
        this.UnparsableToolArguments = arguments;
        return EMPTY_OBJECT;
    }
    
    /// <summary>
    /// Writes the given properties over a copy of the seed.
    /// </summary>
    /// <remarks>
    /// Copying rather than rebuilding keeps whatever the provider sent along that we do not know
    /// about. The values are JSON text, so that a string is escaped exactly once.
    /// </remarks>
    /// <param name="overrides">The properties to write, as property name to JSON text.</param>
    private JsonElement BuildFromSeed(Dictionary<string, string> overrides)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (this.seed.ValueKind is JsonValueKind.Object)
                foreach (var property in this.seed.EnumerateObject())
                {
                    if (overrides.ContainsKey(property.Name))
                        continue;
                    
                    property.WriteTo(writer);
                }
            
            foreach (var (propertyName, json) in overrides)
            {
                writer.WritePropertyName(propertyName);
                using var value = JsonDocument.Parse(json);
                value.RootElement.WriteTo(writer);
            }
            
            writer.WriteEndObject();
        }
        
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
    
    private static string ReadString(JsonElement item, string propertyName)
    {
        if (item.ValueKind is not JsonValueKind.Object ||
            !item.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not JsonValueKind.String)
            return string.Empty;

        return property.GetString() ?? string.Empty;
    }
}