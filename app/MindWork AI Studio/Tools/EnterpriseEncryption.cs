using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools;

/// <summary>
/// Provides encryption and decryption functionality for enterprise configuration plugins.
/// This is used to encrypt/decrypt API keys in Lua configuration files.
/// </summary>
/// <remarks>
/// Important: This is obfuscation, not security. Users with administrative access
/// to their machines can potentially extract the decrypted API keys. This feature
/// is designed to prevent casual exposure of API keys in configuration files. It
/// also protects against accidental leaks while sharing configuration snippets,
/// as the encrypted values cannot be decrypted without the secret key.
/// <br/><br/>
/// Both formats use the same secret. "ENC:v2:" is authenticated: AES-256-GCM refuses
/// every value which was encrypted with another secret or changed afterward. "ENC:v1:"
/// uses AES-256-CBC without authentication, so a wrong secret passes its padding check
/// in about one of 256 cases; decoding the result strictly as UTF-8 catches nearly all
/// of those. Versions before v26.10.1 read only v1, which is why AI Studio still writes
/// it when asked to.
/// </remarks>
public sealed class EnterpriseEncryption
{
    /// <summary>
    /// The number of iterations to derive the key and IV of a v1 value from the password.
    /// We use a higher iteration count here because the secret is static
    /// (not regenerated each startup like the IPC encryption).
    /// </summary>
    private const int V1_ITERATIONS = 10_000;

    /// <summary>
    /// The length of the salt in bytes, in both formats.
    /// </summary>
    private const int SALT_LENGTH = 16;

    /// <summary>
    /// The length of the AES-GCM nonce of a v2 value in bytes.
    /// </summary>
    private const int V2_NONCE_LENGTH = 12;

    /// <summary>
    /// The length of the AES-GCM authentication tag of a v2 value in bytes.
    /// </summary>
    private const int V2_TAG_LENGTH = 16;

    /// <summary>
    /// The prefix all encrypted values share, whatever their format.
    /// </summary>
    private const string ENCRYPTED_PREFIX = "ENC:";

    /// <summary>
    /// The prefix of v1 values.
    /// </summary>
    private const string PREFIX_V1 = "ENC:v1:";

    /// <summary>
    /// The prefix of v2 values.
    /// </summary>
    private const string PREFIX_V2 = "ENC:v2:";

    /// <summary>
    /// What HKDF binds the key of a v2 value to, so that the key serves no other purpose.
    /// </summary>
    private static readonly byte[] V2_KEY_INFO = "MindWork AI Studio ENC:v2"u8.ToArray();

    /// <summary>
    /// The prefix is authenticated together with the ciphertext, so a v2 payload counts under
    /// this prefix only.
    /// </summary>
    private static readonly byte[] V2_ASSOCIATED_DATA = Encoding.UTF8.GetBytes(PREFIX_V2);

    /// <summary>
    /// Decodes UTF-8 and throws on invalid bytes instead of replacing them.
    /// </summary>
    private static readonly UTF8Encoding STRICT_UTF8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ILogger<EnterpriseEncryption> logger;
    private readonly byte[]? secretKey;

    /// <summary>
    /// Gets a value indicating whether the encryption service is available.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Creates a new instance of the enterprise encryption service.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="base64Secret">The base64-encoded 32-byte encryption secret.</param>
    public EnterpriseEncryption(ILogger<EnterpriseEncryption> logger, string? base64Secret)
    {
        this.logger = logger;

        if (string.IsNullOrWhiteSpace(base64Secret))
        {
            this.logger.LogWarning("No enterprise encryption secret configured. Encrypted API keys in configuration plugins will not be available.");
            this.IsAvailable = false;
            return;
        }

        try
        {
            this.secretKey = Convert.FromBase64String(base64Secret);
            if (this.secretKey.Length != 32)
            {
                this.logger.LogWarning($"The enterprise encryption secret must be exactly 32 bytes (256 bits). Got {this.secretKey.Length} bytes.");
                this.secretKey = null;
                this.IsAvailable = false;
                return;
            }

            this.IsAvailable = true;
            this.logger.LogInformation("Enterprise encryption service initialized successfully.");
        }
        catch (FormatException ex)
        {
            this.logger.LogWarning(ex, "Failed to decode the enterprise encryption secret from base64.");
            this.IsAvailable = false;
        }
    }

    /// <summary>
    /// Checks if the given value is encrypted, in whatever format.
    /// </summary>
    /// <remarks>
    /// A format which only a newer version of AI Studio knows counts as encrypted, too. Such a
    /// value cannot be decrypted here, and that is what the user has to learn, rather than that
    /// it was stored in plaintext.
    /// </remarks>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value starts with the encryption prefix; otherwise, false.</returns>
    public static bool IsEncrypted(string? value) => value?.StartsWith(ENCRYPTED_PREFIX, StringComparison.Ordinal) ?? false;

    /// <summary>
    /// Tries to decrypt an encrypted value.
    /// </summary>
    /// <param name="encryptedValue">The encrypted value, with the ENC:v1: or ENC:v2: prefix.</param>
    /// <param name="decryptedValue">When successful, contains the decrypted plaintext.</param>
    /// <returns>True if decryption was successful; otherwise, false.</returns>
    public bool TryDecrypt(string encryptedValue, out string decryptedValue)
    {
        decryptedValue = string.Empty;
        if (!this.IsAvailable)
        {
            this.logger.LogWarning("Cannot decrypt: Enterprise encryption service is not available.");
            return false;
        }

        if (!IsEncrypted(encryptedValue))
        {
            this.logger.LogWarning("Cannot decrypt: Value does not have the expected encryption prefix.");
            return false;
        }

        try
        {
            byte[]? decryptedBytes;
            if (encryptedValue.StartsWith(PREFIX_V2, StringComparison.Ordinal))
                decryptedBytes = this.DecryptV2(Convert.FromBase64String(encryptedValue[PREFIX_V2.Length..]));
            else if (encryptedValue.StartsWith(PREFIX_V1, StringComparison.Ordinal))
                decryptedBytes = this.DecryptV1(Convert.FromBase64String(encryptedValue[PREFIX_V1.Length..]));
            else
            {
                this.logger.LogWarning("Cannot decrypt: The value uses an encryption format which this version of AI Studio does not know. A newer version may have encrypted it.");
                return false;
            }

            if (decryptedBytes is null)
                return false;

            //
            // AI Studio always encrypted valid UTF-8, so decoding strictly never refuses a genuine
            // value. It refuses the random bytes, though, which a wrong secret yields for a v1 value
            // whenever the padding happens to check out:
            //
            decryptedValue = STRICT_UTF8.GetString(decryptedBytes);
            return true;
        }
        catch (FormatException ex)
        {
            this.logger.LogWarning(ex, "Failed to decode encrypted value from base64.");
            return false;
        }
        catch (CryptographicException ex)
        {
            this.logger.LogWarning(ex, "Failed to decrypt value. The encryption secret may be incorrect.");
            return false;
        }
        catch (DecoderFallbackException ex)
        {
            this.logger.LogWarning(ex, "Failed to decrypt value, because the result is no text. The encryption secret may be incorrect.");
            return false;
        }
    }

    /// <summary>
    /// Encrypts a plaintext value.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="format">The format to write. A v2 value is authenticated, but only AI Studio v26.10.1 and later can read it.</param>
    /// <param name="encryptedValue">When successful, contains the encrypted value with prefix.</param>
    /// <returns>True if encryption was successful; otherwise, false.</returns>
    public bool TryEncrypt(string plaintext, EnterpriseEncryptionFormat format, out string encryptedValue)
    {
        encryptedValue = string.Empty;
        if (!this.IsAvailable)
        {
            this.logger.LogWarning("Cannot encrypt: Enterprise encryption service is not available.");
            return false;
        }

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            switch (format)
            {
                case EnterpriseEncryptionFormat.V1:
                    encryptedValue = this.EncryptV1(plaintextBytes);
                    return true;

                case EnterpriseEncryptionFormat.V2:
                    encryptedValue = this.EncryptV2(plaintextBytes);
                    return true;

                default:
                    this.logger.LogWarning($"Cannot encrypt: The encryption format '{format}' is unknown.");
                    return false;
            }
        }
        catch (CryptographicException ex)
        {
            this.logger.LogWarning(ex, "Failed to encrypt value.");
            return false;
        }
    }

    /// <summary>
    /// Generates a new random 32-byte secret key and returns it as a base64 string.
    /// </summary>
    /// <returns>A base64-encoded 32-byte secret key.</returns>
    public static string GenerateSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Encrypts into a v1 value: the salt, followed by the AES-256-CBC ciphertext.
    /// </summary>
    private string EncryptV1(byte[] plaintextBytes)
    {
        // Generate a random salt:
        var salt = RandomNumberGenerator.GetBytes(SALT_LENGTH);

        // Encrypt using AES-256-CBC:
        using var aes = this.CreateAesV1(salt);
        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Combine salt and ciphertext, encode to base64, and add the prefix:
        byte[] combined = [..salt, ..cipherText];
        return PREFIX_V1 + Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Decrypts the payload of a v1 value: the salt, followed by the AES-256-CBC ciphertext.
    /// </summary>
    /// <remarks>
    /// Organizations deployed configuration plugins in this format, so AI Studio reads it for
    /// good, as decided on 2026-10-17. This is no compatibility shim with a removal date.
    /// </remarks>
    /// <returns>The decrypted bytes, or null when the payload is too short.</returns>
    private byte[]? DecryptV1(byte[] encryptedBytes)
    {
        if (encryptedBytes.Length < SALT_LENGTH + 1)
        {
            this.logger.LogWarning("Cannot decrypt: Encrypted data is too short.");
            return null;
        }

        // Extract salt and encrypted content:
        var salt = encryptedBytes[..SALT_LENGTH];
        var cipherText = encryptedBytes[SALT_LENGTH..];

        // Decrypt using AES-256-CBC:
        using var aes = this.CreateAesV1(salt);
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    /// <summary>
    /// Creates the AES-256-CBC instance for a v1 value, with key and IV derived from the secret
    /// and the salt of that value.
    /// </summary>
    private Aes CreateAesV1(byte[] salt)
    {
        // Derive key and IV using PBKDF2:
        using var keyDerivation = new Rfc2898DeriveBytes(this.secretKey!, salt, V1_ITERATIONS, HashAlgorithmName.SHA512);
        var aes = Aes.Create();
        aes.Key = keyDerivation.GetBytes(32); // AES-256
        aes.IV = keyDerivation.GetBytes(16);  // AES block size
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    /// <summary>
    /// Encrypts into a v2 value: the salt, the nonce, the AES-256-GCM ciphertext, and its tag.
    /// </summary>
    private string EncryptV2(byte[] plaintextBytes)
    {
        // The random salt gives every value a key of its own; a random nonce on top costs nothing:
        var salt = RandomNumberGenerator.GetBytes(SALT_LENGTH);
        var nonce = RandomNumberGenerator.GetBytes(V2_NONCE_LENGTH);
        var cipherText = new byte[plaintextBytes.Length];
        var tag = new byte[V2_TAG_LENGTH];

        using var aes = new AesGcm(this.DeriveKeyV2(salt), V2_TAG_LENGTH);
        aes.Encrypt(nonce, plaintextBytes, cipherText, tag, V2_ASSOCIATED_DATA);

        byte[] combined = [..salt, ..nonce, ..cipherText, ..tag];
        return PREFIX_V2 + Convert.ToBase64String(combined);
    }

    /// <summary>
    /// Decrypts the payload of a v2 value: the salt, the nonce, the AES-256-GCM ciphertext, and its tag.
    /// </summary>
    /// <remarks>
    /// Throws an AuthenticationTagMismatchException, which is a CryptographicException, when the
    /// tag does not match: for a wrong secret as well as for a changed value.
    /// </remarks>
    /// <returns>The decrypted bytes, or null when the payload is too short.</returns>
    private byte[]? DecryptV2(byte[] encryptedBytes)
    {
        if (encryptedBytes.Length < SALT_LENGTH + V2_NONCE_LENGTH + V2_TAG_LENGTH)
        {
            this.logger.LogWarning("Cannot decrypt: Encrypted data is too short.");
            return null;
        }

        var salt = encryptedBytes[..SALT_LENGTH];
        var nonce = encryptedBytes[SALT_LENGTH..(SALT_LENGTH + V2_NONCE_LENGTH)];
        var cipherText = encryptedBytes[(SALT_LENGTH + V2_NONCE_LENGTH)..^V2_TAG_LENGTH];
        var tag = encryptedBytes[^V2_TAG_LENGTH..];

        using var aes = new AesGcm(this.DeriveKeyV2(salt), V2_TAG_LENGTH);
        var decryptedBytes = new byte[cipherText.Length];
        aes.Decrypt(nonce, cipherText, tag, decryptedBytes, V2_ASSOCIATED_DATA);
        return decryptedBytes;
    }

    /// <summary>
    /// Derives the AES-256 key of a v2 value from the secret and the salt of that value.
    /// </summary>
    /// <remarks>
    /// Unlike a password, the secret is meant to be 256 random bits, as GenerateSecret makes it.
    /// Stretching it, as PBKDF2 does for v1 values, adds nothing, so HKDF suffices.
    /// </remarks>
    private byte[] DeriveKeyV2(byte[] salt) => HKDF.DeriveKey(HashAlgorithmName.SHA256, this.secretKey!, 32, salt, V2_KEY_INFO);
}
