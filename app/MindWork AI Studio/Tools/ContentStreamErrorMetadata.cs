using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamErrorMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Error")]
    public ContentStreamErrorDetails? Error { get; init; }
}