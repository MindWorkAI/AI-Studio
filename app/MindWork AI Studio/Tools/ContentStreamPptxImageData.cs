using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamPptxImageData
{
    [JsonPropertyName("id")] 
    public string? Id { get; init; }

    [JsonPropertyName("content")] 
    public string? Content { get; init; }

    [JsonPropertyName("segment")] 
    public int? Segment { get; init; }

    [JsonPropertyName("is_end")] 
    public bool IsEnd { get; init; }
}