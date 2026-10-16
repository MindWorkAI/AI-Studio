namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// What the index knows about a data source as a whole, without its files.
/// </summary>
/// <remarks>
/// The manifest answers the same question, but reads every file and every stored failure of the
/// data source to do so. That is the right thing before a run, and far too much for a question
/// asked about several data sources every time somebody opens the data source selection.
///
/// SourceHash is the telling one: it is written once a run has worked through the whole data
/// source, and resetting the index deletes the row it lives in. So an empty hash means no run has
/// finished since the index was last discarded.
/// </remarks>
/// <param name="EmbeddingProviderId">The embedding provider the stored vectors were created with.</param>
/// <param name="EmbeddingSignature">Identifies the embedding configuration the stored vectors belong to.</param>
/// <param name="SourceHash">The hash of the data source as a whole, written when a run completes.</param>
/// <param name="VectorSize">The dimension of the stored vectors.</param>
public sealed record DataSourceIndexState(string EmbeddingProviderId, string EmbeddingSignature, string SourceHash, int VectorSize);