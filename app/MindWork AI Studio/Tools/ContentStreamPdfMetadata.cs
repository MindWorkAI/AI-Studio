using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public class ContentStreamPdfMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Pdf")] 
    public ContentStreamPdfDetails? Pdf { get; init; }
}