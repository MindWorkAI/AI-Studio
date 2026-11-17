using System.Text.Json.Serialization;

namespace AIStudio.Tools;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable ClassNeverInstantiated.Global
public sealed class ContentStreamSpreadsheetDetails
{
    [JsonPropertyName("sheet_name")] 
    public string? SheetName { get; set; }

    [JsonPropertyName("row_number")] 
    public int? RowNumber { get; set; }
}