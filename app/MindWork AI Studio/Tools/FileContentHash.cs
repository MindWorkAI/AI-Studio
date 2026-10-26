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
    /// <remarks>
    /// The file is read while other programs may keep it open, even for writing, as an office
    /// program does with a document being edited. The runtime reads files for extraction the same
    /// way, so a file which can be indexed can always be hashed.
    /// </remarks>
    /// <param name="path">The file path.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static async Task<string> ComputeAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }
}