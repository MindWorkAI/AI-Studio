using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// The hash of the text of a chunk, exactly as it was embedded.
/// </summary>
/// <remarks>
/// A vector depends on nothing but the text it was made from and the embedding signature, and all
/// vectors of one store share that signature. So two chunks of a store with the same hash may share
/// a vector, which is what spares a document the embedding provider for every chunk that did not
/// change. The text is hashed with its overlap and its heading line, as the provider received it.
/// </remarks>
internal static class ChunkTextHash
{
    /// <summary>
    /// SHA-256 over the UTF-8 bytes of the text, as lowercase hex.
    /// </summary>
    /// <param name="text">The text of the chunk.</param>
    /// <returns>The hash.</returns>
    public static string Of(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}