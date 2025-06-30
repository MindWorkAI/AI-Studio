using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamTextMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Text")]
    public ContentStreamTextDetails? Text { get; init; }
}