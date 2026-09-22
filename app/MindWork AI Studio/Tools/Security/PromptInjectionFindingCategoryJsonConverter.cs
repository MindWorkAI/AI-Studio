using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.Security;

/// <summary>
/// Reads the finding category in the snake_case spelling the Rust runtime sends.
/// </summary>
/// <remarks>
/// The converter sits on the enum itself because neither path that reads a finding passes
/// JsonSerializerOptions: the sanitize response is read by RustService.SanitizePromptInjections
/// and the content stream by RustService.ReadFileContent. The shared RustEnumConverter therefore
/// never applies here, and without a converter on the type only numbers would be accepted.
///
/// An unrecognized category falls back to UNKNOWN instead of throwing. Throwing would cost more
/// than the label: it fails the whole response, and the guard service then passes the content
/// through unfiltered rather than losing a single name.
/// </remarks>
public sealed class PromptInjectionFindingCategoryJsonConverter : JsonConverter<PromptInjectionFindingCategory>
{
    private static readonly ILogger<PromptInjectionFindingCategoryJsonConverter> LOG = Program.LOGGER_FACTORY.CreateLogger<PromptInjectionFindingCategoryJsonConverter>();

    public override PromptInjectionFindingCategory Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            LOG.LogWarning("Cannot read a prompt injection finding category from a '{TokenType}' token. Using UNKNOWN.", reader.TokenType);
            return PromptInjectionFindingCategory.UNKNOWN;
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            LOG.LogWarning("Read an empty prompt injection finding category. Using UNKNOWN.");
            return PromptInjectionFindingCategory.UNKNOWN;
        }

        //
        // The enum members are the wire value in upper case, so upper-casing replaces a naming
        // policy. Values starting with a digit or sign are rejected up front, because Enum.TryParse
        // would otherwise accept "0" or "-1" as a category:
        //
        if (!char.IsAsciiDigit(text[0]) && text[0] is not ('-' or '+') && Enum.TryParse<PromptInjectionFindingCategory>(text.ToUpperInvariant(), out var category))
            return category;

        LOG.LogWarning("The runtime reported the unknown prompt injection finding category '{Category}'. Using UNKNOWN.", text);
        return PromptInjectionFindingCategory.UNKNOWN;
    }

    public override void Write(Utf8JsonWriter writer, PromptInjectionFindingCategory value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString().ToLowerInvariant());
}