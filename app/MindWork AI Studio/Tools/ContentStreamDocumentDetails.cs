using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamDocumentDetails
{
    [JsonPropertyName("page_number")]
    public int? PageNumber { get; init; }

    [JsonPropertyName("image")]
    public ContentStreamPptxImageData? Image { get; init; }
}
