using AIStudio.Tools;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the encryption of the API keys, tokens, and passwords which organizations deploy with
/// their configuration plugins.
/// </summary>
/// <remarks>
/// Organizations deployed configuration plugins with values in the "ENC:v1:" format, and AI Studio
/// keeps reading them. The legacy values here were therefore built without AI Studio: PBKDF2 with
/// SHA-512 and 10,000 iterations derives 48 bytes from the secret and the salt, the first 32 being
/// the AES-256 key and the last 16 the IV, and openssl encrypted the text with AES-256-CBC. A change
/// which breaks one of these values breaks every plugin deployed with that format.
/// </remarks>
[TestFixture]
public sealed class EnterpriseEncryptionTests
{
    /// <summary>
    /// The encryption secret of the legacy values below, as an organization deploys it.
    /// </summary>
    private const string LEGACY_SECRET = "/af/yJrG1dl9xGt26GcAlXMZ2+drG5I1w5KvIJgJnUk=";

    [TestCase("ENC:v1:nj0LJPcEvqzGau4SRDw4W94R2hLJBiB+FJ8gySll+vA=", "", TestName = "An empty legacy value")]
    [TestCase("ENC:v1:xc6EIl2jhKUODkp1Nr83kYmE86Vcti23dzSStSpbMts=", "sk-imported-key", TestName = "A legacy value within one block")]
    [TestCase("ENC:v1:K5fUwDVwDkNDTm3NZHuY53uhKvzQ6AZJqWAX/0dn1vvXGfz6cjbIOLhATWSoAg+U", "Schlüssel für Größe 🔑", TestName = "A legacy value beyond ASCII")]
    [TestCase("ENC:v1:LlgaFe4LWq9tuULA8Ysx3GVjmTxrNWPOggY5mKOGj+H2M14BBC+Y37U/nvSnOMqdiuUFAO6zJzwjNenZL0nb5kZX2co4u6Q+VAqrJ9u/VYM/GR4MNYYXJqelNbGGj71n", "eri-token-0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", TestName = "A legacy value over several blocks")]
    public void LegacyValuesStayReadable(string encryptedValue, string expectedValue)
    {
        var encryption = new EnterpriseEncryption(NullLogger<EnterpriseEncryption>.Instance, LEGACY_SECRET);

        Assert.Multiple(() =>
        {
            Assert.That(EnterpriseEncryption.IsEncrypted(encryptedValue), Is.True);
            Assert.That(encryption.TryDecrypt(encryptedValue, out var decryptedValue), Is.True);
            Assert.That(decryptedValue, Is.EqualTo(expectedValue));
        });
    }
}