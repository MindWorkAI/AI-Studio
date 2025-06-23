using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

public class SseEvent
{
    [JsonPropertyName("content")] 
    public string Content { get; set; }

    [JsonPropertyName("metadata")] 
    public Metadata Metadata { get; set; }
}

[JsonConverter(typeof(MetadataJsonConverter))]
public abstract class Metadata;

public class TextMetadata : Metadata
{
    [JsonPropertyName("Text")]
    public TextDetails Text { get; set; }
}

public class TextDetails
{
    [JsonPropertyName("line_number")]
    public int LineNumber { get; set; }
}

public class PdfMetadata : Metadata
{
    [JsonPropertyName("Pdf")] 
    public PdfDetails Pdf { get; set; }
}

public class PdfDetails
{
    [JsonPropertyName("page_number")] 
    public int PageNumber { get; set; }
}

public class SpreadsheetMetadata : Metadata
{
    [JsonPropertyName("Spreadsheet")] 
    public SpreadsheetDetails Spreadsheet { get; set; }
}

public class SpreadsheetDetails
{
    [JsonPropertyName("sheet_name")] 
    public string SheetName { get; set; }

    [JsonPropertyName("row_number")] 
    public int RowNumber { get; set; }
}

public class DocumentMetadata : Metadata {}

public class PresentationMetadata : Metadata
{
    [JsonPropertyName("Presentation")] 
    public PresentationDetails Presentation { get; set; }
}

public class PresentationDetails
{
    [JsonPropertyName("slide_number")] 
    public int SlideNumber { get; set; }

    [JsonPropertyName("image")] 
    public ImageData Image { get; set; }
}

public class ImageMetadata : Metadata
{
    [JsonPropertyName("Image")] 
    public object Image { get; set; }
}

public class ImageData
{
    [JsonPropertyName("id")] 
    public string Id { get; set; }

    [JsonPropertyName("content")] 
    public string Content { get; set; }

    [JsonPropertyName("segment")] 
    public int Segment { get; set; }

    [JsonPropertyName("is_end")] 
    public bool IsEnd { get; set; }
}