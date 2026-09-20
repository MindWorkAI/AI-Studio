using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Reads a streamed Responses API call back into the response the tool calling loop works with.
/// </summary>
/// <remarks>
/// The API repeats the whole response when it is done, reasoning items included, so nothing has
/// to be reassembled from fragments: that closing event is the round. What this type does beyond
/// taking it is hand out text and sources while they arrive, and keep the finished output items
/// as a fallback for gateways which never send that closing event.<br/><br/>
/// No HTTP, no dependency injection, no provider: everything here is a decision about bytes, and
/// those are the decisions worth having a test for.
/// </remarks>
public sealed class ResponsesStreamAccumulator
{
    private const string EVENT_COMPLETED = "response.completed";
    private const string EVENT_TEXT_DELTA = "response.output_text.delta";
    private const string EVENT_ANNOTATION_ADDED = "response.output_text.annotation.added";
    private const string EVENT_OUTPUT_ITEM_DONE = "response.output_item.done";
    
    private readonly List<JsonElement> completedOutputItems = [];
    private ResponsesResponse? completedResponse;
    
    /// <summary>
    /// Takes the next event of the stream and returns what it has to show.
    /// </summary>
    /// <param name="serverSentEvent">The event to read.</param>
    /// <returns>The text and sources of this event, both empty when it carried neither.</returns>
    public ResponsesStreamPart Process(ServerSentEvent serverSentEvent)
    {
        if (serverSentEvent.Data.Length is 0)
            return ResponsesStreamPart.Nothing;

        string eventType;
        try
        {
            using var document = JsonDocument.Parse(serverSentEvent.Data);
            var root = document.RootElement;
            if (root.ValueKind is not JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeProperty) ||
                typeProperty.ValueKind is not JsonValueKind.String)
                return ResponsesStreamPart.Nothing;

            eventType = typeProperty.GetString() ?? string.Empty;
            
            //
            // The item is cloned because its document is disposed at the end of this block, and
            // an element which outlives its document reads memory that is no longer there.
            //
            if (eventType is EVENT_OUTPUT_ITEM_DONE && root.TryGetProperty("item", out var outputItem))
                this.completedOutputItems.Add(outputItem.Clone());
        }
        catch (JsonException)
        {
            // A line we cannot read is a line we skip, exactly as the plain text path does:
            return ResponsesStreamPart.Nothing;
        }

        switch (eventType)
        {
            case EVENT_COMPLETED:
                this.completedResponse = TryDeserialize<ResponsesCompletedStreamLine>(serverSentEvent.Data)?.Response ?? this.completedResponse;
                return ResponsesStreamPart.Nothing;
            
            case EVENT_TEXT_DELTA:
                var deltaLine = TryDeserialize<ResponsesDeltaStreamLine>(serverSentEvent.Data);
                if (deltaLine is null || !deltaLine.ContainsContent())
                    return ResponsesStreamPart.Nothing;

                return new ResponsesStreamPart(deltaLine.GetContent().Content, []);
            
            case EVENT_ANNOTATION_ADDED:
                var annotationLine = TryDeserialize<ResponsesAnnotationStreamLine>(serverSentEvent.Data);
                if (annotationLine is null || !annotationLine.ContainsSources())
                    return ResponsesStreamPart.Nothing;

                return new ResponsesStreamPart(string.Empty, annotationLine.GetSources());
            
            default:
                return ResponsesStreamPart.Nothing;
        }
    }
    
    /// <summary>
    /// Builds the response of the round from everything the stream said.
    /// </summary>
    /// <returns>
    /// The response, or null when the stream ended before it said anything usable. Null is how a
    /// failed request and a truncated stream look from here, and both end the round.
    /// </returns>
    public ResponsesResponse? Build()
    {
        if (this.completedResponse is not null)
            return this.completedResponse;

        if (this.completedOutputItems.Count is 0)
            return null;

        //
        // No closing event came, so the round is put back together from the items which did.
        // Reasoning items are among them, which is what the next request needs to continue.
        //
        return new ResponsesResponse
        {
            Output = [..this.completedOutputItems],
        };
    }
    
    private static T? TryDeserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, ProviderJsonOptions.OPTIONS);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}