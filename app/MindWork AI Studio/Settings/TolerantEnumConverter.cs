using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Settings;

/// <summary>
/// Tries to convert a JSON string to an enum value.
/// </summary>
/// <remarks>
/// When the target enum value does not exist, the value will be the default value.
/// This converter handles enum values as property names and values.
/// </remarks>
public sealed class TolerantEnumConverter : JsonConverter<object>
{
    private static readonly ILogger<TolerantEnumConverter> LOG = Program.LOGGER_FACTORY.CreateLogger<TolerantEnumConverter>();
    
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Is this token a string?
        if (reader.TokenType == JsonTokenType.String)
            // Try to use that string as the name of the enum value:
            if (Enum.TryParse(typeToConvert, reader.GetString(), out var result))
                return result;

        // In any other case, we will return the default enum value:
        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{typeToConvert.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(typeToConvert);
    }

    public override object ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Is this token a property name?
        if (reader.TokenType == JsonTokenType.PropertyName)
            // Try to use that property name as the name of the enum value:
            if (Enum.TryParse(typeToConvert, reader.GetString(), out var result))
                return result;

        // In any other case, we will return the default enum value:
        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{typeToConvert.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(typeToConvert)!;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
    
    public override void WriteAsPropertyName(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString()!);
    }
}