namespace AIStudio.Tools;

public static class EncryptedTextExtensions
{
    public static async Task<EncryptedText> Encrypt(this string data, Encryption encryption) => await encryption.Encrypt(data);

    public static async Task<string> Decrypt(this EncryptedText encryptedText, Encryption encryption) => await encryption.Decrypt(encryptedText);
}