using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public class MetadataJsonConverter : JsonConverter<SseMetadata>
{
    public override SseMetadata? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;
        var rawText = root.GetRawText();
        
        var propertyName = root.EnumerateObject()
            .Select(p => p.Name)
            .FirstOrDefault();

        return propertyName switch
        {
            "Text" => JsonSerializer.Deserialize<TextMetadata?>(rawText, options),
            "Pdf" => JsonSerializer.Deserialize<PdfMetadata?>(rawText, options),
            "Spreadsheet" => JsonSerializer.Deserialize<SpreadsheetMetadata?>(rawText, options),
            "Presentation" => JsonSerializer.Deserialize<PresentationMetadata?>(rawText, options),
            "Image" => JsonSerializer.Deserialize<ImageMetadata?>(rawText, options),
            "Document" => JsonSerializer.Deserialize<DocumentMetadata?>(rawText, options),
            
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, SseMetadata value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}