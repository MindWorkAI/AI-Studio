using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.Services;

/// <summary>
/// Converts enum values for Rust communication.
/// </summary>
/// <remarks>
/// Rust expects PascalCase enum values (e.g., "VoiceRecordingToggle"),
/// while .NET uses UPPER_SNAKE_CASE (e.g., "VOICE_RECORDING_TOGGLE").
/// This converter handles the bidirectional conversion.
/// </remarks>
public sealed class RustEnumConverter : JsonConverter<object>
{
    private static readonly ILogger<RustEnumConverter> LOG = Program.LOGGER_FACTORY.CreateLogger<RustEnumConverter>();

    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override object? Read(ref Utf8JsonReader reader, Type enumType, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            text = ConvertToUpperSnakeCase(text);

            if (Enum.TryParse(enumType, text, out var result))
                return result;
        }

        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{enumType.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(enumType);
    }

    public override object ReadAsPropertyName(ref Utf8JsonReader reader, Type enumType, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.PropertyName)
        {
            var text = reader.GetString();
            text = ConvertToUpperSnakeCase(text);

            if (Enum.TryParse(enumType, text, out var result))
                return result;
        }

        LOG.LogWarning($"Cannot read '{reader.GetString()}' as '{enumType.Name}' enum; token type: {reader.TokenType}");
        return Activator.CreateInstance(enumType)!;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ConvertToPascalCase(value.ToString()));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(ConvertToPascalCase(value.ToString()));
    }

    /// <summary>
    /// Converts UPPER_SNAKE_CASE to PascalCase.
    /// </summary>
    /// <param name="text">The text to convert (e.g., "VOICE_RECORDING_TOGGLE").</param>
    /// <returns>The converted text as PascalCase (e.g., "VoiceRecordingToggle").</returns>
    private static string ConvertToPascalCase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var parts = text.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;

            // First character uppercase, rest lowercase:
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part[1..].ToLowerInvariant());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a string to UPPER_SNAKE_CASE.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The converted text as UPPER_SNAKE_CASE.</returns>
    private static string ConvertToUpperSnakeCase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        var lastCharWasLowerCase = false;

        foreach (var c in text)
        {
            if (char.IsUpper(c) && lastCharWasLowerCase)
                sb.Append('_');

            sb.Append(char.ToUpperInvariant(c));
            lastCharWasLowerCase = char.IsLower(c);
        }

        return sb.ToString();
    }
}
