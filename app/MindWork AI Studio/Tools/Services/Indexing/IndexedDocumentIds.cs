using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// The ids under which documents and their chunks are stored.
/// </summary>
/// <remarks>
/// Derived rather than random, so the same document of the same data source ends up under the same
/// ids, in the vector store as well as in the index store. Once stored, they are the identity of the
/// document: a file which moves to another folder keeps its ids, although they were derived from its
/// old path and its old fingerprint, cf. FileSourceIndexer.RelocateStoredFilesAsync. The formats must
/// stay as they are: every stored id was made with them.
/// </remarks>
internal static class IndexedDocumentIds
{
    /// <summary>
    /// The id of one chunk, which is also the id of its point in the vector store.
    /// </summary>
    /// <param name="dataSourceId">The data source the document belongs to.</param>
    /// <param name="source">What the chunk ids of the document derive from, cf. CreateChunkIdSource.</param>
    /// <param name="chunkIndex">The position of the chunk within the document.</param>
    /// <returns>The chunk id.</returns>
    public static string CreateChunkId(string dataSourceId, string source, int chunkIndex) =>
        CreateStableGuid($"{dataSourceId}:chunk:{source}:{chunkIndex}");

    /// <summary>
    /// What the chunk ids of a document derive from: nearly always its fingerprint, and the generation of the chunks.
    /// </summary>
    /// <remarks>
    /// A moved file keeps its parent id and its chunk ids, but its fingerprint now names the new path.
    /// Another file may later appear at the old path with exactly the fingerprint those chunk ids came
    /// from, and the fingerprint alone would hand it the same ids. The two documents would then take
    /// over each other's chunks. Hence the parent id joins whenever a document is not stored under the
    /// id its key gives, which only happens after a move. Every other document keeps the ids it has.<br/><br/>
    /// Every time a document is indexed again, its chunks get the next generation, and with it new
    /// ids, even where the fingerprint stays the same, as for a mail. So the new chunks never
    /// overwrite the old ones, which keep their vectors until the new chunks are stored, cf.
    /// ReusableVectors. The first indexing of a document is generation 0, which gives the ids
    /// documents got before generations existed.
    /// </remarks>
    /// <param name="dataSourceId">The data source the document belongs to.</param>
    /// <param name="documentKey">The key of the document.</param>
    /// <param name="parentId">The id the document is stored under.</param>
    /// <param name="fingerprint">The fingerprint of the document as it is about to be read.</param>
    /// <param name="generation">The generation of the chunks about to be written.</param>
    /// <returns>What to pass to CreateChunkId.</returns>
    public static string CreateChunkIdSource(string dataSourceId, string documentKey, string parentId, string fingerprint, int generation = 0)
    {
        var source = string.Equals(parentId, CreateParentId(dataSourceId, documentKey), StringComparison.Ordinal)
            ? fingerprint
            : $"{parentId}:{fingerprint}";

        return generation is 0 ? source : $"{source}:generation:{generation}";
    }

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