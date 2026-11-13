using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamSseEvent
{
    [JsonPropertyName("content")] 
    public string? Content { get; set; }
    
    [JsonPropertyName("stream_id")] 
    public string? StreamId { get; set; }

    [JsonPropertyName("metadata")] 
    public ContentStreamSseMetadata? Metadata { get; set; }

    [JsonPropertyName("token_count")]
    public int? TokenCount { get; set; }
}