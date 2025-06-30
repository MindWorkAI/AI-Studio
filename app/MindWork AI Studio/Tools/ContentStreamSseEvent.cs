using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamSseEvent
{
    [JsonPropertyName("content")] 
    public string? Content { get; init; }
    
    [JsonPropertyName("stream_id")] 
    public string? StreamId { get; init; }

    [JsonPropertyName("metadata")] 
    public ContentStreamSseMetadata? Metadata { get; init; }
}