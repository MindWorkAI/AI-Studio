using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamSpreadsheetMetadata : ContentStreamSseMetadata
{
    [JsonPropertyName("Spreadsheet")] 
    public ContentStreamSpreadsheetDetails? Spreadsheet { get; init; }
}