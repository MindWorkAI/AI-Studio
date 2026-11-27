using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Stores the password encoding of a mailbox by its name, e.g., "windows-1252".
/// </summary>
/// <remarks>
/// A name this version does not know is read as it is and written back unchanged, cf.
/// MailboxPasswordEncoding.IsKnown. Anything else than a name, e.g., a number written by hand, is no
/// encoding at all: the mailbox then signs in nowhere, but the other settings are read as usual.
/// </remarks>
public sealed class MailboxPasswordEncodingJsonConverter : JsonConverter<MailboxPasswordEncoding>
{
    /// <inheritdoc />
    public override MailboxPasswordEncoding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String)
            return MailboxPasswordEncoding.FromName(reader.GetString());

        reader.Skip();
        return MailboxPasswordEncoding.FromName(null);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, MailboxPasswordEncoding value, JsonSerializerOptions options) => writer.WriteStringValue(value.Name);
}