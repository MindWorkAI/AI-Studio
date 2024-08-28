using System.Security;

namespace AIStudio.Tools;

public readonly record struct EncryptedText(string EncryptedData)
{
    public EncryptedText() : this(string.Empty)
    {
        throw new SecurityException("Please provide the encrypted data.");
    }
}