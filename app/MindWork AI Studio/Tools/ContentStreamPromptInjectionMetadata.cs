using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamPromptInjectionMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("PromptInjection")]
    public ContentStreamPromptInjectionDetails? PromptInjection { get; init; }
}