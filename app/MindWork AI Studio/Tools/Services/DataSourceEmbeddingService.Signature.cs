using System.Globalization;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tools.Services;

public sealed partial class DataSourceEmbeddingService
{
    internal const int DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH = 300;

    /// <summary>
    /// The version of the chunk metadata, part of every embedding signature and frozen at 2. Raise it
    /// only for a change which makes every stored vector wrong.
    /// </summary>
    /// <remarks>
    /// Raising this number makes the embedding signature of every data source differ, files and
    /// mailboxes alike. That drops every index and sends every document to the embedding provider
    /// once more, which is right only when no stored vector is valid anymore, e.g., because every
    /// chunk would get another text. A better way of reading some files, or a new value stored next
    /// to a chunk, does not justify it: a chunk which lacks the value reads as not knowing it, and
    /// gains it once its document is indexed again.
    ///
    /// Version 2: the page of a chunk is taken from the runtime metadata instead of being read back
    /// out of the chunk text, which is what left Word and OpenDocument files, and passages
    /// continuing across a page break, without a page. It rebuilt every mailbox as well, although
    /// mails have no pages, and fetched every mail from the server once more. That is why this
    /// number is frozen.
    ///
    /// Not raised for the page a chunk ends on, which chunks store since v26.10.1. It adds to what
    /// a chunk says and corrects nothing: a chunk without it still names its start page, which is
    /// right, and gains its end page once its file is indexed again. Rebuilding every index for it
    /// would send every document to the embedding provider once more, for a page range.
    /// </remarks>
    private const string CHUNK_METADATA_VERSION = "2";

    /// <summary>
    /// The version of what this build makes of a mail before it is cut into chunks, part of the
    /// embedding signature of every mailbox and frozen at 2. Raise it only for a change which makes
    /// every stored vector of a mailbox wrong.
    /// </summary>
    /// <remarks>
    /// The counterpart of CHUNK_METADATA_VERSION for mailboxes alone. A mail on the server never
    /// changes, so nothing reads it again once it is indexed, however differently MailTextBuilder
    /// would write it today. Raising this number rebuilds the index of every mailbox, which fetches
    /// every mail from the server and embeds it once more, and leaves every other data source alone.
    /// A change which alters the text of some mails, such as those with attachments, does not
    /// justify that.
    ///
    /// Version 1: the header block and the text from the HTML part, without attachments.
    ///
    /// Version 2: the text of attached documents follows, each attachment under a line naming it.
    /// The signature of a signed mail no longer counts as an attachment.
    /// </remarks>
    private const string MAIL_TEXT_VERSION = "2";

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
    /// The file patterns and the exclusion list of a directory, and whether it includes subfolders
    /// and hidden files and folders, are omitted as well. They change which documents belong to the
    /// source, not how retained documents are embedded; the file indexer adds and removes the
    /// affected document rows and vectors during its ordinary hash-check run. A run which still works
    /// with the old selection stops all the same, cf. BuildSelectionSignature.
    ///
    /// The confidence level a data source asks of a provider is deliberately not among them. It
    /// changes no vector, and it is enforced live on every request anyway: DataSourceService checks
    /// it against the participating chat providers and against the embedding provider, and this
    /// service checks it again before each indexing run. It was part of this signature once, which
    /// re-embedded every file of a data source whenever somebody raised or lowered it — real money
    /// at a cloud embedding provider, for nothing.
    ///
    /// A mailbox appends the version of its mail text, cf. MAIL_TEXT_VERSION, and up to which size
    /// it reads attachments. A mail is never read again once it is indexed, so reading attachments
    /// from now on, or larger ones, reaches the mails indexed before only by indexing them anew. The
    /// dialog asks before it does that, as for every change of this signature. Nothing else is
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

        if (dataSource is not DataSourceMailbox mailbox)
            return signature;

        var attachments = MailAttachmentRules.GetMaxSizeMegabytes(mailbox) is { } maxSizeMegabytes
            ? maxSizeMegabytes.ToString(CultureInfo.InvariantCulture)
            : "none";

        return $"{signature}|mail:{MAIL_TEXT_VERSION}|attachments:{attachments}";
    }

    /// <summary>
    /// Describes how the vectors of a data source were made, working the chunking out along the way.
    /// </summary>
    /// <param name="dataSource">The data source the vectors belong to.</param>
    /// <param name="embeddingProvider">The embedding provider which makes them.</param>
    /// <returns>The signature of this pairing.</returns>
    internal static string BuildEmbeddingSignature(IDataSourceBase dataSource, EmbeddingProvider embeddingProvider) =>
        BuildEmbeddingSignature(dataSource, embeddingProvider, GetChunkingOptions(dataSource, embeddingProvider));

    /// <summary>
    /// Describes which documents of a data source an indexing run works with.
    /// </summary>
    /// <remarks>
    /// Kept out of the embedding signature on purpose, cf. BuildEmbeddingSignature: another selection
    /// adds and removes documents, while every vector stored so far stays valid. A run which started
    /// with another selection, however, goes on with it until it ends, perhaps after hours: it embeds
    /// files which no longer belong to the data source, and the ones which now do have to wait for
    /// the next run.
    ///
    /// Local files and folders include their configured paths, so relocating one stops a run which
    /// still reads the old location. Folders also include their file selection. Neither changes the
    /// embedding signature: unchanged documents keep their vectors.
    /// </remarks>
    /// <param name="dataSource">The data source to index.</param>
    /// <returns>The description of its selection, or an empty string.</returns>
    internal static string BuildSelectionSignature(IIndexedDataSource dataSource) => dataSource switch
    {
        DataSourceLocalDirectory directory => $"{directory.Path.Length}:{directory.Path}|{FolderFileSelection.Of(directory).Describe()}",
        DataSourceLocalFile file => $"{file.FilePath.Length}:{file.FilePath}",
        _ => string.Empty,
    };

    /// <summary>
    /// Describes the indexing run a data source would get with a given embedding provider.
    /// </summary>
    /// <remarks>
    /// The embedding signature and the selection signature, as long as the provider may read the
    /// data source at all. Comparing what a run started with against what it would start with now
    /// tells whether the run is outdated: it embeds for an index the next run throws away, it works
    /// with documents the data source no longer selects, or it sends documents to a provider which
    /// may no longer read them.
    /// </remarks>
    /// <param name="dataSource">The data source to index.</param>
    /// <param name="embeddingProvider">The embedding provider the data source names.</param>
    /// <param name="embeddingProviderConfidence">The confidence level of that embedding provider.</param>
    /// <returns>The signature of the run, or NONE when the provider may not read the data source and no run would start.</returns>
    internal static DataSourceRunSignature GetRunSignature(IIndexedDataSource dataSource, EmbeddingProvider embeddingProvider, ConfidenceLevel embeddingProviderConfidence) =>
        AllowsEmbedding(dataSource, embeddingProviderConfidence)
            ? new(BuildEmbeddingSignature(dataSource, embeddingProvider), BuildSelectionSignature(dataSource))
            : DataSourceRunSignature.NONE;

    /// <summary>
    /// Whether an indexing run works for embedding settings or a selection of documents which no longer apply.
    /// </summary>
    /// <param name="runSignature">The signature the run started with, or null while the run has not resolved its embedding provider yet.</param>
    /// <param name="currentRunSignature">The signature a run would start with now, or NONE when no run would start anymore.</param>
    /// <returns>True when the run should stop.</returns>
    internal static bool IsRunOutdated(DataSourceRunSignature? runSignature, DataSourceRunSignature currentRunSignature)
    {
        //
        // Still preparing, so nothing was embedded or sent anywhere yet. The run checks for itself
        // once it records its signature.
        //
        if (runSignature is null)
            return false;

        return currentRunSignature == DataSourceRunSignature.NONE || runSignature != currentRunSignature;
    }
}