using System.Security.Cryptography;

namespace AIStudio.Tools;

/// <summary>
/// Computes the hash of the content of a file.
/// </summary>
internal static class FileContentHash
{
    /// <summary>
    /// Computes a lowercase SHA-256 hash for a file without loading it fully into memory.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static async Task<string> ComputeAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }
}