using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamPresentationMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Presentation")] 
    public ContentStreamPresentationDetails? Presentation { get; init; }
}