using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class ContentStreamMetadataJsonConverter : JsonConverter<ContentStreamSseMetadata>
{
    public override ContentStreamSseMetadata? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;
        var rawText = root.GetRawText();
        
        var propertyName = root.EnumerateObject()
            .Select(p => p.Name)
            .FirstOrDefault();

        return propertyName switch
        {
            "Text" => JsonSerializer.Deserialize<ContentStreamTextMetadata?>(rawText, options),
            "Pdf" => JsonSerializer.Deserialize<ContentStreamPdfMetadata?>(rawText, options),
            "Spreadsheet" => JsonSerializer.Deserialize<ContentStreamSpreadsheetMetadata?>(rawText, options),
            "Presentation" => JsonSerializer.Deserialize<ContentStreamPresentationMetadata?>(rawText, options),
            "Image" => JsonSerializer.Deserialize<ContentStreamImageMetadata?>(rawText, options),
            "Document" => JsonSerializer.Deserialize<ContentStreamDocumentMetadata?>(rawText, options),
            
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, ContentStreamSseMetadata value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}