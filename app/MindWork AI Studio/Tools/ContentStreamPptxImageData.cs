using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamPptxImageData
{
    [JsonPropertyName("id")] 
    public string? Id { get; set; }

    [JsonPropertyName("content")] 
    public string? Content { get; set; }

    [JsonPropertyName("segment")] 
    public int? Segment { get; set; }

    [JsonPropertyName("is_end")] 
    public bool IsEnd { get; set; }

    [JsonPropertyName("media_type")]
    public string? MediaType { get; set; }
}
