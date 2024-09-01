using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools;

public sealed class EncryptedTextJsonConverter : JsonConverter<EncryptedText>
{
    #region Overrides of JsonConverter<EncryptedText>

    public override EncryptedText Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
        {
            var value = reader.GetString()!;
            return new EncryptedText(value);
        }
        
        throw new JsonException($"Unexpected token type when parsing EncryptedText. Expected {JsonTokenType.String}, but got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, EncryptedText value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.EncryptedData);
    }

    #endregion
}