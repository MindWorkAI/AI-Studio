using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamPdfDetails
{
    [JsonPropertyName("page_number")] 
    public int? PageNumber { get; init; }
}