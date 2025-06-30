using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamPresentationDetails
{
    [JsonPropertyName("slide_number")] 
    public int? SlideNumber { get; init; }

    [JsonPropertyName("image")] 
    public ContentStreamPptxImageData? Image { get; init; }
}