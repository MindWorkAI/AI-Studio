namespace AIStudio.Tools;

public static class EncryptedTextExtensions
{
    extension(string data)
    {
        public async Task<EncryptedText> Encrypt(Encryption encryption) => await encryption.Encrypt(data);
    }

    extension(EncryptedText encryptedText)
    {
        public async Task<string> Decrypt(Encryption encryption) => await encryption.Decrypt(encryptedText);
    }
}