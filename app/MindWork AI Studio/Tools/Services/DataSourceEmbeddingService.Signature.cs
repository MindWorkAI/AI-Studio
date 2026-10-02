using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    internal const int DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH = 300;

    /// <summary>
    /// What this build writes next to a chunk besides its text. Raise it whenever that changes.
    /// </summary>
    /// <remarks>
    /// A stored chunk keeps the metadata of the run which wrote it, and nothing recomputes it: the
    /// fingerprint of a file says whether the file changed, not whether we got better at reading
    /// it. Raising this number makes the embedding signature differ, which drops the index and
    /// builds it again — the only way corrected page numbers reach a data source somebody indexed
    /// earlier.
    ///
    /// Version 2: the page of a chunk is taken from the runtime metadata instead of being read back
    /// out of the chunk text, which is what left Word and OpenDocument files, and passages
    /// continuing across a page break, without a page.
    /// </remarks>
    private const string CHUNK_METADATA_VERSION = "2";

    /// <summary>
    /// What this build makes of a mail before it is cut into chunks. Raise it whenever that changes.
    /// </summary>
    /// <remarks>
    /// The counterpart of CHUNK_METADATA_VERSION for mailboxes alone. A mail on the server never
    /// changes, so nothing reads it again once it is indexed, however differently MailTextBuilder
    /// would write it today. Raising this number rebuilds the index of every mailbox and leaves
    /// every other data source alone.
    ///
    /// Version 1: the header block and the text from the HTML part, without attachments.
    /// </remarks>
    private const string MAIL_TEXT_VERSION = "1";

    /// <summary>
    /// Works out how the text of a data source is cut for a given embedding provider.
    /// </summary>
    /// <remarks>
    /// Static, because the answer follows from its two arguments alone. That lets the embedding
    /// signature be built for a configuration which is not stored yet, which is what the dialogs ask
    /// before they save a change.
    /// </remarks>
    /// <param name="dataSource">The data source whose own chunk settings apply.</param>
    /// <param name="embeddingProvider">The embedding provider whose token limit caps them.</param>
    /// <returns>The chunk size and overlap which are actually used.</returns>
    internal static ChunkingOptions GetChunkingOptions(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider)
    {
        var providerMaxChunkTokenLength = Math.Max(1, embeddingProvider.EffectiveTokenLimit);
        var dataSourceMaxChunkTokenLength = dataSource is IIndexedDataSource { MaxChunkTokenLength: > 0 } indexedDataSource
            ? indexedDataSource.MaxChunkTokenLength
            : 0;
        var maxChunkTokenLength = dataSourceMaxChunkTokenLength > 0
            ? Math.Min(dataSourceMaxChunkTokenLength, providerMaxChunkTokenLength)
            : providerMaxChunkTokenLength;

        var configuredOverlapTokenLength = dataSource is IIndexedDataSource overlapDataSource
            ? overlapDataSource.ChunkOverlapTokenLength
            : DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
        var overlapTokenLength = Math.Clamp(configuredOverlapTokenLength, 0, Math.Max(0, maxChunkTokenLength - 1));

        return new(maxChunkTokenLength, overlapTokenLength);
    }

    /// <summary>
    /// Describes how the vectors of a data source were made.
    /// </summary>
    /// <remarks>
    /// What appears here decides when stored embeddings are thrown away: a signature differing from
    /// the persisted one drops the whole index and builds it again. So it names the embedding model,
    /// where it runs, how the text was cut for it, and the chunk metadata version — the things a
    /// vector actually depends on.
    ///
    /// Two of them are less obvious than they look. The Hugging Face inference provider belongs to
    /// where the model runs: the same model name served by another backend is another vector source.
    /// And a custom tokenizer enters through its content, not through its path, because a tokenizer
    /// is stored under the name it came with — almost always tokenizer.json — so swapping one for
    /// another lands on the identical path, while moving the data directory changes every path
    /// without changing a single tokenizer.
    ///
    /// The chunk settings enter only as what they amount to, never as what somebody typed. A data
    /// source storing 0 means "follow the embedding provider", and writing that provider's own limit
    /// into the field changes nothing about how the text is cut. Carrying the typed numbers as well
    /// made that a different signature, so opening the expert settings of a data source — which
    /// fills an empty limit with the provider's — threw the whole index away for nothing.
    ///
    /// The confidence level a data source asks of a provider is deliberately not among them. It
    /// changes no vector, and it is enforced live on every request anyway: DataSourceService checks
    /// it against the participating chat providers and against the embedding provider, and this
    /// service checks it again before each indexing run. It was part of this signature once, which
    /// re-embedded every file of a data source whenever somebody raised or lowered it — real money
    /// at a cloud embedding provider, for nothing.
    ///
    /// A mailbox appends the version of its mail text, cf. MAIL_TEXT_VERSION. Nothing else is
    /// appended for the other kinds of data source, so their stored signatures stay valid.
    /// </remarks>
    internal static string BuildEmbeddingSignature(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider, ChunkingOptions chunkingOptions)
    {
        var signature = string.Join('|',
            CHUNK_METADATA_VERSION,
            embeddingProvider.Id,
            embeddingProvider.UsedLLMProvider,
            embeddingProvider.Model.Id,
            embeddingProvider.Host,
            embeddingProvider.Hostname,
            embeddingProvider.HFInferenceProvider,
            embeddingProvider.TokenizerFingerprint,
            embeddingProvider.EffectiveTokenLimit,
            chunkingOptions.MaxChunkTokenLength,
            chunkingOptions.OverlapTokenLength);

        return dataSource is DataSourceMailbox ? $"{signature}|mail:{MAIL_TEXT_VERSION}" : signature;
    }

    /// <summary>
    /// Describes how the vectors of a data source were made, working the chunking out along the way.
    /// </summary>
    /// <param name="dataSource">The data source the vectors belong to.</param>
    /// <param name="embeddingProvider">The embedding provider which makes them.</param>
    /// <returns>The signature of this pairing.</returns>
    internal static string BuildEmbeddingSignature(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider) =>
        BuildEmbeddingSignature(dataSource, embeddingProvider, GetChunkingOptions(dataSource, embeddingProvider));
}