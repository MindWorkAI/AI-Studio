using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamTextDetails
{
    [JsonPropertyName("line_number")]
    public int? LineNumber { get; init; }
}