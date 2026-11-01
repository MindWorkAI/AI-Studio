using AIStudio.Tools;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the encryption which the app shares with the runtime, for the secrets which travel
/// between both.
/// </summary>
/// <remarks>
/// The runtime encrypts and decrypts the same values in Rust, with a password and a salt which it
/// makes anew at every start. Both sides therefore have to derive exactly the same key and IV. The
/// values here were built without AI Studio, the way the runtime builds them: PBKDF2 with SHA-512 and
/// 100 iterations derives 48 bytes from the password and the salt, the first 32 being the AES-256 key
/// and the last 16 the IV. openssl encrypted the text with AES-256-CBC, and the salt precedes the
/// ciphertext.
/// </remarks>
[TestFixture]
public sealed class EncryptionTests
{
    private static readonly byte[] SECRET_PASSWORD = Enumerable.Range(0, 512).Select(index => (byte)index).ToArray();

    private static readonly byte[] SECRET_KEY_SALT = Enumerable.Range(0xA0, 16).Select(index => (byte)index).ToArray();

    private static async Task<Encryption> CreateEncryptionAsync()
    {
        var encryption = new Encryption(NullLogger<Encryption>.Instance, SECRET_PASSWORD, SECRET_KEY_SALT);
        await encryption.Initialize();
        return encryption;
    }

    [TestCase("oKGio6SlpqeoqaqrrK2ur3lNRzNAfoSYKyC70CpjwS8=", "", TestName = "An empty text")]
    [TestCase("oKGio6SlpqeoqaqrrK2urwUfAfn5LbbIjbOC9nSvtUU=", "sk-imported-key", TestName = "A text within one block")]
    [TestCase("oKGio6SlpqeoqaqrrK2urwbjgbJCNk6Zw1Z9HyIiJdZJ+E7czzLYlM/undXUIsDr", "Schlüssel für Größe 🔑", TestName = "A text beyond ASCII")]
    public async Task TheAppEncryptsLikeTheRuntime(string expectedEncryptedValue, string text)
    {
        var encryption = await CreateEncryptionAsync();

        var encryptedText = await encryption.Encrypt(text);
        Assert.That(encryptedText.EncryptedData, Is.EqualTo(expectedEncryptedValue));
    }

    [TestCase("oKGio6SlpqeoqaqrrK2ur3lNRzNAfoSYKyC70CpjwS8=", "", TestName = "An empty encrypted text")]
    [TestCase("oKGio6SlpqeoqaqrrK2urwUfAfn5LbbIjbOC9nSvtUU=", "sk-imported-key", TestName = "An encrypted text within one block")]
    [TestCase("oKGio6SlpqeoqaqrrK2urwbjgbJCNk6Zw1Z9HyIiJdZJ+E7czzLYlM/undXUIsDr", "Schlüssel für Größe 🔑", TestName = "An encrypted text beyond ASCII")]
    public async Task TheAppDecryptsWhatTheRuntimeEncrypted(string encryptedValue, string expectedText)
    {
        var encryption = await CreateEncryptionAsync();

        var text = await encryption.Decrypt(new EncryptedText(encryptedValue));
        Assert.That(text, Is.EqualTo(expectedText));
    }
}