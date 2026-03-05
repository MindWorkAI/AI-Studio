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
/// </remarks>
public sealed class EnterpriseEncryption
{
    /// <summary>
    /// The number of iterations to derive the key and IV from the password.
    /// We use a higher iteration count here because the secret is static
    /// (not regenerated each startup like the IPC encryption).
    /// </summary>
    private const int ITERATIONS = 10_000;

    /// <summary>
    /// The length of the salt in bytes.
    /// </summary>
    private const int SALT_LENGTH = 16;

    /// <summary>
    /// The prefix for encrypted values.
    /// </summary>
    private const string PREFIX = "ENC:v1:";

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
    /// Checks if the given value is encrypted (has the encryption prefix).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value starts with the encryption prefix; otherwise, false.</returns>
    public static bool IsEncrypted(string? value) => value?.StartsWith(PREFIX, StringComparison.Ordinal) ?? false;

    /// <summary>
    /// Tries to decrypt an encrypted value.
    /// </summary>
    /// <param name="encryptedValue">The encrypted value (with ENC:v1: prefix).</param>
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
            // Extract the base64-encoded data after the prefix:
            var base64Data = encryptedValue[PREFIX.Length..];
            var encryptedBytes = Convert.FromBase64String(base64Data);
            if (encryptedBytes.Length < SALT_LENGTH + 1)
            {
                this.logger.LogWarning("Cannot decrypt: Encrypted data is too short.");
                return false;
            }

            // Extract salt and encrypted content:
            var salt = encryptedBytes[..SALT_LENGTH];
            var cipherText = encryptedBytes[SALT_LENGTH..];

            // Derive key and IV using PBKDF2:
            using var keyDerivation = new Rfc2898DeriveBytes(this.secretKey!, salt, ITERATIONS, HashAlgorithmName.SHA512);
            var key = keyDerivation.GetBytes(32); // AES-256
            var iv = keyDerivation.GetBytes(16);  // AES block size

            // Decrypt using AES-256-CBC:
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            decryptedValue = Encoding.UTF8.GetString(decryptedBytes);

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
    }

    /// <summary>
    /// Encrypts a plaintext value.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <param name="encryptedValue">When successful, contains the encrypted value with prefix.</param>
    /// <returns>True if encryption was successful; otherwise, false.</returns>
    public bool TryEncrypt(string plaintext, out string encryptedValue)
    {
        encryptedValue = string.Empty;
        if (!this.IsAvailable)
        {
            this.logger.LogWarning("Cannot encrypt: Enterprise encryption service is not available.");
            return false;
        }

        try
        {
            // Generate a random salt:
            var salt = RandomNumberGenerator.GetBytes(SALT_LENGTH);

            // Derive key and IV using PBKDF2:
            using var keyDerivation = new Rfc2898DeriveBytes(this.secretKey!, salt, ITERATIONS, HashAlgorithmName.SHA512);
            var key = keyDerivation.GetBytes(32); // AES-256
            var iv = keyDerivation.GetBytes(16);  // AES block size

            // Encrypt using AES-256-CBC:
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherText = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Combine salt and ciphertext
            var combined = new byte[SALT_LENGTH + cipherText.Length];
            Array.Copy(salt, 0, combined, 0, SALT_LENGTH);
            Array.Copy(cipherText, 0, combined, SALT_LENGTH, cipherText.Length);

            // Encode to base64 and add the prefix:
            encryptedValue = PREFIX + Convert.ToBase64String(combined);
            return true;
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
}
