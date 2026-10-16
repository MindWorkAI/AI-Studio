using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamDocumentMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Document")]
    public ContentStreamDocumentDetails? Document { get; init; }
}
