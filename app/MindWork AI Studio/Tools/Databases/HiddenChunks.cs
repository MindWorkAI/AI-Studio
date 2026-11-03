namespace AIStudio.Tools.Databases;

/// <summary>
/// The chunks of one document which no search may find.
/// </summary>
/// <remarks>
/// A document is searched by the generation its index row names, and only once that generation is
/// complete. Indexing it again writes the next generation next to it, and an attempt which failed
/// may stay there afterward, so the next one can reuse its vectors, cf. IndexedRunContext. A
/// document indexed for the first time has no searchable generation until its first one is
/// complete. The index store knows all of this, and the vector store leaves out what it names.
/// </remarks>
/// <param name="ParentFileId">The parent id of the document.</param>
/// <param name="SearchableGeneration">The generation which can be searched, so every other one is hidden. Null while the document has none, so all of its chunks are hidden.</param>
public sealed record HiddenChunks(string ParentFileId, int? SearchableGeneration);