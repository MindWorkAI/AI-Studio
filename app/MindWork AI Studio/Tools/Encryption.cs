using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools;

public sealed class Encryption(ILogger<Encryption> logger, byte[] secretPassword, byte[] secretKeySalt)
{
    /// <summary>
    /// The number of iterations to derive the key and IV from the password. For a password manager
    /// where the user has to enter their primary password, 100 iterations would be too few and
    /// insecure. Here, the use case is different: We generate a 512-byte long and cryptographically
    /// secure password at every start. This password already contains enough entropy. In our case,
    /// we need key and IV primarily because AES, with the algorithms we chose, requires a fixed key
    /// length, and our password is too long.
    /// </summary>
    private const int ITERATIONS = 100;
    
    private readonly byte[] key = new byte[32];
    private readonly byte[] iv = new byte[16];

    public async Task Initialize()
    { 
        logger.LogInformation("Initializing encryption service...");
        var stopwatch = Stopwatch.StartNew();

        if (secretPassword.Length != 512)
        {
            logger.LogError($"The secret password must be 512 bytes long. It was {secretPassword.Length} bytes long.");
            throw new CryptographicException("The secret password must be 512 bytes long.");
        }
        
        if(secretKeySalt.Length != 16)
        {
            logger.LogError($"The salt data must be 16 bytes long. It was {secretKeySalt.Length} bytes long.");
            throw new CryptographicException("The salt data must be 16 bytes long.");
        }

        // Derive key and iv vector: the operations take several seconds. Thus, using a task:
        await Task.Run(() =>
        {
            using var keyVectorObj = new Rfc2898DeriveBytes(secretPassword, secretKeySalt, ITERATIONS, HashAlgorithmName.SHA512);
            var keyBytes = keyVectorObj.GetBytes(32); // the max valid key length = 256 bit = 32 bytes
            var ivBytes = keyVectorObj.GetBytes(16); // the only valid block size = 128 bit = 16 bytes
            
            Array.Copy(keyBytes, this.key, this.key.Length);
            Array.Copy(ivBytes, this.iv, this.iv.Length);
        });

        var initDuration = stopwatch.Elapsed;
        
        stopwatch.Stop();
        logger.LogInformation($"Encryption service initialized in {initDuration.TotalMilliseconds} milliseconds.");
    }

    public async Task<EncryptedText> Encrypt(string data)
    {
        // Create AES encryption:
        using var aes = Aes.Create();
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = this.key;
        aes.IV = this.iv;
        aes.Mode = CipherMode.CBC;

        using var encryption = aes.CreateEncryptor();

        // Copy the given string data into a memory stream:
        await using var plainDataStream = new MemoryStream(Encoding.UTF8.GetBytes(data));

        // A memory stream for the final, encrypted data:
        await using var encryptedAndEncodedData = new MemoryStream();
        
        // A base64 stream for the encoding:
        await using var base64Stream = new CryptoStream(encryptedAndEncodedData, new ToBase64Transform(), CryptoStreamMode.Write);

        // Write the salt into the base64 stream:
        await base64Stream.WriteAsync(secretKeySalt);

        // Create the encryption stream:
        await using var cryptoStream = new CryptoStream(base64Stream, encryption, CryptoStreamMode.Write);

        // Write the payload into the encryption stream:
        await plainDataStream.CopyToAsync(cryptoStream);
        
        // Flush the final block. Please note that it is not enough to call the regular flush method.
        await cryptoStream.FlushFinalBlockAsync();
        
        // Convert the base64 encoded data back into a string. Uses GetBuffer due to the advantage that
        // it does not create another copy of the data. ToArray would create another copy of the data.
        return new EncryptedText(Encoding.ASCII.GetString(encryptedAndEncodedData.GetBuffer()[..(int)encryptedAndEncodedData.Length]));
    }

    public async Task<string> Decrypt(EncryptedText encryptedData)
    {
        // Build a memory stream to access the given base64 encoded data:
        await using var encodedEncryptedStream = new MemoryStream(Encoding.ASCII.GetBytes(encryptedData.EncryptedData));

        // Wrap around the base64 decoder stream:
        await using var base64Stream = new CryptoStream(encodedEncryptedStream, new FromBase64Transform(), CryptoStreamMode.Read);

        // A buffer for the salt's bytes:
        var readSaltBytes = new byte[16]; // 16 bytes = Guid
        
        // Read the salt's bytes out of the stream:
        var readBytes = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        while(readBytes < readSaltBytes.Length && !cts.Token.IsCancellationRequested)
        {
            readBytes += await base64Stream.ReadAsync(readSaltBytes, readBytes, readSaltBytes.Length - readBytes, cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(60), cts.Token);
        }
        
        // Check the salt bytes:
        if(!readSaltBytes.SequenceEqual(secretKeySalt))
        {
            logger.LogError("The salt bytes do not match. The data is corrupted or tampered.");
            throw new CryptographicException("The salt bytes do not match. The data is corrupted or tampered.");
        }

        // Create AES decryption:
        using var aes = Aes.Create();
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = this.key;
        aes.IV = this.iv;

        using var decryption = aes.CreateDecryptor();

        // A memory stream for the final, decrypted data:
        await using var decryptedData = new MemoryStream();

        // The crypto stream:
        await using var cryptoStream = new CryptoStream(base64Stream, decryption, CryptoStreamMode.Read);
        
        // Reads all remaining data through the decrypt stream. Note that this operation
        // starts at the current position, i.e., after the salt bytes:
        await cryptoStream.CopyToAsync(decryptedData);

        // Convert the decrypted data back into a string. Uses GetBuffer due to the advantage that
        // it does not create another copy of the data. ToArray would create another copy of the data.
        return Encoding.UTF8.GetString(decryptedData.GetBuffer()[..(int)decryptedData.Length]);
    }
}