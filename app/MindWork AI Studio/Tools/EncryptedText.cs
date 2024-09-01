using System.Security;
using System.Text.Json.Serialization;

namespace AIStudio.Tools;

[JsonConverter(typeof(EncryptedTextJsonConverter))]
public readonly record struct EncryptedText(string EncryptedData)
{
    public EncryptedText() : this(string.Empty)
    {
        throw new SecurityException("Please provide the encrypted data.");
    }
}