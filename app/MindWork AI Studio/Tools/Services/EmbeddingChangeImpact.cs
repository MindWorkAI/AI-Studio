using AIStudio.Settings;

namespace AIStudio.Tools.Services;

/// <summary>
/// Answers whether an edit throws the stored index of a data source away.
/// </summary>
/// <remarks>
/// Nothing here knows which settings matter. Both questions are answered by building the embedding
/// signature twice and comparing the two, so the single place which decides stays
/// BuildEmbeddingSignature and this cannot drift away from what an indexing run then does.
/// </remarks>
internal static class EmbeddingChangeImpact
{
    /// <summary>
    /// Whether an edited embedding provider invalidates what is stored for one of its data sources.
    /// </summary>
    /// <param name="dataSource">The data source, which the edit leaves alone.</param>
    /// <param name="before">The embedding provider as it is stored.</param>
    /// <param name="after">The embedding provider as it would be stored.</param>
    /// <returns>True when the stored index would be discarded.</returns>
    public static bool AffectsStoredIndex(IDataSource dataSource, EmbeddingProvider before, EmbeddingProvider after) =>
        !string.Equals(
            DataSourceEmbeddingService.BuildEmbeddingSignature(dataSource, before),
            DataSourceEmbeddingService.BuildEmbeddingSignature(dataSource, after),
            StringComparison.Ordinal);

    /// <summary>
    /// Whether an edited data source invalidates what is stored for it.
    /// </summary>
    /// <remarks>
    /// Each side is asked with the embedding provider it points at, never both with the same one. A
    /// data source carries only the id of its provider, while the signature carries what that provider
    /// is, so comparing both sides against one of them would report a changed embedding as no change
    /// at all -- and the next indexing run would then rebuild everything unannounced.
    /// </remarks>
    /// <param name="before">The data source as it is stored.</param>
    /// <param name="beforeProvider">The embedding provider it points at today.</param>
    /// <param name="after">The data source as it would be stored.</param>
    /// <param name="afterProvider">The embedding provider it would point at.</param>
    /// <returns>True when the stored index would be discarded.</returns>
    public static bool AffectsStoredIndex(IDataSource before, EmbeddingProvider beforeProvider, IDataSource after, EmbeddingProvider afterProvider) =>
        !string.Equals(
            DataSourceEmbeddingService.BuildEmbeddingSignature(before, beforeProvider),
            DataSourceEmbeddingService.BuildEmbeddingSignature(after, afterProvider),
            StringComparison.Ordinal);
}