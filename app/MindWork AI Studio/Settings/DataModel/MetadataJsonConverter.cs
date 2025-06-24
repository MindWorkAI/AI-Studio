using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Settings.DataModel;

public class MetadataJsonConverter : JsonConverter<Metadata>
{
    private static readonly Dictionary<string, Type> TYPE_MAP = new()
    {
        { "Text", typeof(TextMetadata) },
        { "Pdf", typeof(PdfMetadata) },
        { "Spreadsheet", typeof(SpreadsheetMetadata) },
        { "Presentation", typeof(PresentationMetadata) },
        { "Image", typeof(ImageMetadata) },
        { "Document", typeof(DocumentMetadata) }
    };

    public override Metadata? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;
        var rawText = root.GetRawText();
        
        var propertyName = root.EnumerateObject()
            .Select(p => p.Name)
            .FirstOrDefault(name => TYPE_MAP.ContainsKey(name));
            
        if (propertyName != null && TYPE_MAP.TryGetValue(propertyName, out var metadataType))
        {
            return (Metadata?)JsonSerializer.Deserialize(rawText, metadataType, options);
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Metadata value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}