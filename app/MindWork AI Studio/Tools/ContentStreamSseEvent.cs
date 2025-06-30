using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public class ContentStreamSseEvent
{
    [JsonPropertyName("content")] 
    public string? Content { get; init; }

    [JsonPropertyName("metadata")] 
    public ContentStreamSseMetadata? Metadata { get; init; }
}