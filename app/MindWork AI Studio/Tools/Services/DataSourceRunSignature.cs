namespace AIStudio.Tools.Services;

/// <summary>
/// The embedding settings an indexing run of a data source works for.
/// </summary>
/// <remarks>
/// Deliberately nothing but the embedding signature, which decides when a stored index is thrown
/// away. A run counts as outdated exactly when the next run would throw its work away, and a list of
/// fields of its own would have to be kept in step with that signature by hand.
/// </remarks>
/// <param name="EmbeddingSignature">The embedding signature of the run, cf. DataSourceEmbeddingService.BuildEmbeddingSignature.</param>
internal sealed record DataSourceRunSignature(string EmbeddingSignature)
{
    /// <summary>
    /// No run would start: the data source or its embedding provider is gone, or that provider may not read it.
    /// </summary>
    public static readonly DataSourceRunSignature NONE = new(string.Empty);
}