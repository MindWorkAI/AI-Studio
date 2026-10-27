namespace AIStudio.Tools.Services;

/// <summary>
/// What an indexing run of a data source works for: its embedding settings and the documents it selects.
/// </summary>
/// <remarks>
/// A run counts as outdated when the next run would throw its work away, or when it works with
/// documents which no longer belong to the data source, or misses the ones which now do. Both parts
/// are descriptions which exist anyway, so no list of fields has to be kept in step with them by
/// hand: the embedding signature decides when a stored index is thrown away, and the selection
/// signature is what the selection of a folder tells about itself.
/// </remarks>
/// <param name="EmbeddingSignature">The embedding signature of the run, cf. DataSourceEmbeddingService.BuildEmbeddingSignature.</param>
/// <param name="SelectionSignature">Which documents the run works with, cf. DataSourceEmbeddingService.BuildSelectionSignature.</param>
internal sealed record DataSourceRunSignature(string EmbeddingSignature, string SelectionSignature)
{
    /// <summary>
    /// No run would start: the data source or its embedding provider is gone, or that provider may not read it.
    /// </summary>
    public static readonly DataSourceRunSignature NONE = new(string.Empty, string.Empty);
}