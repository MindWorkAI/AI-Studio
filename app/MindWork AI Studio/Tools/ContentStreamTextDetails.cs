using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable ClassNeverInstantiated.Global
public class ContentStreamTextDetails
{
    [JsonPropertyName("line_number")]
    public int? LineNumber { get; init; }
}