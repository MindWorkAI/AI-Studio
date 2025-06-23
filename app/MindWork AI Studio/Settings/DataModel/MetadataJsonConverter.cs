using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

public class MetadataJsonConverter : JsonConverter<Metadata>
{
    public override Metadata? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        
        using (var jsonDoc = JsonDocument.ParseValue(ref reader))
        {
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("Text", out _))
            {
                return JsonSerializer.Deserialize<TextMetadata>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("Pdf", out _))
            {
                return JsonSerializer.Deserialize<PdfMetadata>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("Spreadsheet", out _))
            {
                return JsonSerializer.Deserialize<SpreadsheetMetadata>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("Presentation", out _))
            {
                return JsonSerializer.Deserialize<PresentationMetadata>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("Image", out _))
            {
                return JsonSerializer.Deserialize<ImageMetadata>(root.GetRawText(), options);
            }
            else if (root.TryGetProperty("Document", out _))
            {
                return JsonSerializer.Deserialize<DocumentMetadata>(root.GetRawText(), options);
            }
            else
            {
                // Unbekannter Typ
                return null;
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, Metadata value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}