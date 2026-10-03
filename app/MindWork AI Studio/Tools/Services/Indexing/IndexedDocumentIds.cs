using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The ids under which documents and their chunks are stored.
/// </summary>
/// <remarks>
/// Derived rather than random, so the same document of the same data source always ends up under
/// the same ids, in the vector store as well as in the index store. The formats must stay as they
/// are: every stored id was made with them.
/// </remarks>
internal static class IndexedDocumentIds
{
    /// <summary>
    /// The id of one chunk, which is also the id of its point in the vector store.
    /// </summary>
    /// <param name="dataSourceId">The data source the document belongs to.</param>
    /// <param name="fingerprint">The fingerprint of the document as it was read.</param>
    /// <param name="chunkIndex">The position of the chunk within the document.</param>
    /// <returns>The chunk id.</returns>
    public static string CreateChunkId(string dataSourceId, string fingerprint, int chunkIndex) =>
        CreateStableGuid($"{dataSourceId}:chunk:{fingerprint}:{chunkIndex}");

    /// <summary>
    /// The id of a document's row in the index store, which its chunks point at.
    /// </summary>
    /// <param name="dataSourceId">The data source the document belongs to.</param>
    /// <param name="absolutePath">What the row is stored under. For a file, its absolute path.</param>
    /// <returns>The document id.</returns>
    public static string CreateParentId(string dataSourceId, string absolutePath) =>
        CreateStableGuid($"{dataSourceId}:parent-file:{absolutePath}");

    private static string CreateStableGuid(string source)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var guidBytes = hash[..16].ToArray();

        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes).ToString();
    }
}