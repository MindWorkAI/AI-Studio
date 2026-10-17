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
/// <br/><br/>
/// The authenticated "ENC:v2:" format has to refuse every wrong secret and every change. Its tests
/// try thousands of secrets, made up from a fixed seed, so that a failure can be repeated.
/// </remarks>
[TestFixture]
public sealed class EnterpriseEncryptionTests
{
    private const string API_KEY = "sk-imported-key";

    /// <summary>
    /// The seed of the secrets which the tests make up.
    /// </summary>
    private const int SEED = 20_261_017;

    private static readonly string[] PLAINTEXTS =
    [
        string.Empty,
        API_KEY,
        "Schlüssel für Größe 🔑",
        new('x', 1_000),
    ];

    /// <summary>
    /// The encryption secret of the legacy values below, as an organization deploys it.
    /// </summary>
    private const string LEGACY_SECRET = "/af/yJrG1dl9xGt26GcAlXMZ2+drG5I1w5KvIJgJnUk=";

    /// <summary>
    /// A wrong secret for the legacy value of "sk-imported-key", found by searching. With it, the
    /// padding of that value checks out, as it does for about one wrong secret in 256.
    /// </summary>
    private const string WRONG_LEGACY_SECRET = "YXSD2dZLmSU6fEGDpkx9XWEu2+IJi8vG4x2oh49k81E=";

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

    [Test]
    public void ALegacyValueWithAWrongSecretIsRefusedWhenItIsNoText()
    {
        // The legacy format has no authentication, so this wrong secret passes its only check. What
        // it yields are random bytes, though, and AI Studio always encrypted valid UTF-8:
        var encryption = new EnterpriseEncryption(NullLogger<EnterpriseEncryption>.Instance, WRONG_LEGACY_SECRET);

        Assert.That(encryption.TryDecrypt("ENC:v1:xc6EIl2jhKUODkp1Nr83kYmE86Vcti23dzSStSpbMts=", out _), Is.False, "Random bytes must never become an API key.");
    }

    [Test]
    public void EachFormatSurvivesTheRoundTrip([Values] EnterpriseEncryptionFormat format, [ValueSource(nameof(PLAINTEXTS))] string plaintext)
    {
        var encryption = Create(NextSecret(new Random(SEED)));
        Assert.That(encryption.TryEncrypt(plaintext, format, out var encryptedValue), Is.True);
        Assert.That(encryption.TryEncrypt(plaintext, format, out var encryptedAgain), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(encryptedValue, Does.StartWith(PrefixOf(format)));
            Assert.That(EnterpriseEncryption.IsEncrypted(encryptedValue), Is.True);
            Assert.That(encryptedAgain, Is.Not.EqualTo(encryptedValue), "Every value gets a salt of its own, so equal secrets do not show as equal values.");
            Assert.That(encryption.TryDecrypt(encryptedValue, out var decryptedValue), Is.True);
            Assert.That(decryptedValue, Is.EqualTo(plaintext));
        });
    }

    [Test]
    public void NoWrongSecretDecryptsAnAuthenticatedValue()
    {
        var random = new Random(SEED);
        var rightSecret = NextSecret(random);
        var encryptedValue = EncryptAuthenticated(rightSecret, API_KEY);

        // 2,000 secrets at random, plus the 256 which differ from the right one in a single bit:
        var wrongSecrets = Enumerable.Range(0, 2_000).Select(_ => NextSecret(random)).Concat(BitFlipsOf(rightSecret)).ToList();
        var acceptingSecrets = wrongSecrets.Where(wrongSecret => Create(wrongSecret).TryDecrypt(encryptedValue, out _)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(wrongSecrets, Has.Count.EqualTo(2_256));
            Assert.That(Create(rightSecret).TryDecrypt(encryptedValue, out var decryptedValue), Is.True, "Otherwise, this test would pass for nothing.");
            Assert.That(decryptedValue, Is.EqualTo(API_KEY));
            Assert.That(acceptingSecrets, Is.Empty, "In the legacy format, about nine of these secrets would pass the padding check.");
        });
    }

    [Test]
    public void ATamperedValueIsRefused()
    {
        var secret = NextSecret(new Random(SEED));
        var encryption = Create(secret);
        var payload = Convert.FromBase64String(EncryptAuthenticated(secret, API_KEY)["ENC:v2:".Length..]);

        // Salt, nonce, ciphertext, and tag: a change anywhere must be noticed.
        var acceptedPositions = Enumerable.Range(0, payload.Length).Where(position =>
        {
            var tampered = (byte[])payload.Clone();
            tampered[position] ^= 0x01;
            return encryption.TryDecrypt($"ENC:v2:{Convert.ToBase64String(tampered)}", out _);
        }).ToList();

        Assert.That(acceptedPositions, Is.Empty);
    }

    [TestCase("ENC:v2:", TestName = "An authenticated value without payload")]
    [TestCase("ENC:v2:AAAA", TestName = "An authenticated value too short for salt, nonce, and tag")]
    [TestCase("ENC:v1:AAAA", TestName = "A legacy value too short for its salt")]
    [TestCase("ENC:v2:not base64!", TestName = "A value which is no base64")]
    [TestCase("ENC:v2:LlgaFe4LWq9tuULA8Ysx3GVjmTxrNWPOggY5mKOGj+H2M14BBC+Y37U/nvSnOMqdiuUFAO6zJzwjNenZL0nb5kZX2co4u6Q+VAqrJ9u/VYM/GR4MNYYXJqelNbGGj71n", TestName = "A legacy payload passed off as authenticated")]
    [TestCase("ENC:v3:AAAA", TestName = "A format of a newer version")]
    public void AMalformedValueIsRefused(string encryptedValue)
    {
        var encryption = new EnterpriseEncryption(NullLogger<EnterpriseEncryption>.Instance, LEGACY_SECRET);

        Assert.Multiple(() =>
        {
            Assert.That(EnterpriseEncryption.IsEncrypted(encryptedValue), Is.True, "Whatever starts like an encrypted value must not count as plaintext.");
            Assert.That(encryption.TryDecrypt(encryptedValue, out var decryptedValue), Is.False);
            Assert.That(decryptedValue, Is.Empty);
        });
    }

    private static EnterpriseEncryption Create(string secret) => new(NullLogger<EnterpriseEncryption>.Instance, secret);

    private static string EncryptAuthenticated(string secret, string plaintext)
    {
        Assert.That(Create(secret).TryEncrypt(plaintext, EnterpriseEncryptionFormat.V2, out var encryptedValue), Is.True);
        return encryptedValue;
    }

    private static string NextSecret(Random random)
    {
        var secret = new byte[32];
        random.NextBytes(secret);
        return Convert.ToBase64String(secret);
    }

    private static IEnumerable<string> BitFlipsOf(string secret)
    {
        var bytes = Convert.FromBase64String(secret);
        for (var bit = 0; bit < bytes.Length * 8; bit++)
        {
            var flipped = (byte[])bytes.Clone();
            flipped[bit / 8] ^= (byte)(1 << (bit % 8));
            yield return Convert.ToBase64String(flipped);
        }
    }

    private static string PrefixOf(EnterpriseEncryptionFormat format) => format switch
    {
        EnterpriseEncryptionFormat.V1 => "ENC:v1:",
        EnterpriseEncryptionFormat.V2 => "ENC:v2:",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "This test does not know the format yet."),
    };
}