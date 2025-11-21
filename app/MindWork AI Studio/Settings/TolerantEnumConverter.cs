using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Settings;

/// <summary>
/// Tries to convert a JSON string to an enum value.
/// </summary>
/// <remarks>
/// When the target enum value does not exist, the value will be the default value.
/// This converter handles enum values as property names and values.
/// <br/><br/>
/// We assume that enum names are in UPPER_SNAKE_CASE, and the JSON strings may be
/// in any case style (e.g., camelCase, PascalCase, snake_case, UPPER_SNAKE_CASE, etc.)
/// </remarks>
public sealed class TolerantEnumConverter : JsonConverter<object>
{
    private static readonly ILogger<TolerantEnumConverter> LOG = Program.LOGGER_FACTORY.CreateLogger<TolerantEnumConverter>();
    
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override object? Read(ref Utf8JsonReader reader, Type enumType, JsonSerializerOptions options)
    {
        // Is this token a string?
        if (reader.TokenType == JsonTokenType.String)
        {
            // Try to use that string as the name of the enum value:
            var text = reader.GetString();
            
            // Convert the text to UPPER_SNAKE_CASE:
            text = ConvertToUpperSnakeCase(text);
            
            // Try to parse the enum value:
            if (Enum.TryParse(enumType, text, out var result))
                return result;
        }

        // In any other case, we will return the default enum value:
        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{enumType.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(enumType);
    }

    public override object ReadAsPropertyName(ref Utf8JsonReader reader, Type enumType, JsonSerializerOptions options)
    {
        // Is this token a property name?
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            // Try to use that property name as the name of the enum value:
            var text = reader.GetString();
            
            // Convert the text to UPPER_SNAKE_CASE:
            text = ConvertToUpperSnakeCase(text);
            
            // Try to parse the enum value:
            if (Enum.TryParse(enumType, text, out var result))
                return result;
        }

        // In any other case, we will return the default enum value:
        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{enumType.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(enumType)!;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
    
    public override void WriteAsPropertyName(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString()!);
    }
    
    /// <summary>
    /// Converts a string to UPPER_SNAKE_CASE.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The converted text as UPPER_SNAKE_CASE.</returns>
    private static string ConvertToUpperSnakeCase(string? text)
    {
        // Handle null or empty strings:
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        
        // Create a string builder with the same length as the
        // input text. We will add underscores as needed, which
        // may increase the length -- we cannot predict how many
        // underscores will be added, so we just start with the
        // original length:
        var sb = new StringBuilder(text.Length);
        
        // State to track if the last character was lowercase.
        // This helps to determine when to add underscores:
        var lastCharWasLowerCase = false;
        
        // Iterate through each character in the input text:
        foreach(var c in text)
        {
            // If the current character is uppercase and the last
            // character was lowercase, we need to add an underscore:
            if (char.IsUpper(c) && lastCharWasLowerCase)
                sb.Append('_');
			
            // Append the uppercase version of the current character:
            sb.Append(char.ToUpperInvariant(c));
            
            // Keep track of whether the current character is lowercase:
            lastCharWasLowerCase = char.IsLower(c);
        }
	
        return sb.ToString();
    }
}