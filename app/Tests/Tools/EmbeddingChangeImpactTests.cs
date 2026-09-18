using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which edits have to be asked about before they are saved.
/// </summary>
/// <remarks>
/// An edit which changes the embedding signature throws away everything indexed for the data sources
/// behind it, and sends every one of their documents to the embedding provider again. Asking about an
/// edit which costs nothing trains people to click the question away; not asking about one which does
/// costs them money at a cloud provider. So both directions are pinned down here.
/// </remarks>
[TestFixture]
public sealed class EmbeddingChangeImpactTests
{
    [Test]
    public void HarmlessEmbeddingProviderEditsKeepTheStoredIndex()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider();

        Assert.Multiple(() =>
        {
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Name = "Another name" }), Is.False, "The name of an embedding provider reaches no vector.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Num = 42 }), Is.False, "The number is there to sort the list with.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { EmbeddingBatchSize = 16 }), Is.False, "How many chunks travel in one request says nothing about the vectors which come back.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { CustomIconDataUrl = "data:image/png;base64,AAAA" }), Is.False, "An icon is an icon.");
        });
    }

    [Test]
    public void ChangingTheModelDropsTheStoredIndex()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider();

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Model = new("text-embedding-3-large", "text-embedding-3-large") }),
            Is.True,
            "Another model means another vector space.");
    }

    [Test]
    public void ChangingTheTokenLimitDropsTheStoredIndex()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider();

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { TokenLimit = 4096 }),
            Is.True,
            "The token limit decides where the text is cut, and other chunks are other vectors.");
    }

    [Test]
    public void ChangingWhereTheProviderRunsDropsTheStoredIndex()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider();

        Assert.Multiple(() =>
        {
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Hostname = "http://localhost:9999" }), Is.True, "Another server can serve another model under the same name.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Host = Host.LM_STUDIO }), Is.True, "Another kind of host speaks another API.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { HFInferenceProvider = HFInferenceProvider.GROQ }), Is.True, "The same model name served by another backend is another vector source.");
        });
    }

    [Test]
    public void TheTokenizerIsComparedByItsContentNotItsPath()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider() with { TokenizerPath = "/data/tokenizers/embeddings/tokenizer.json", TokenizerFingerprint = "AAAA" };

        Assert.Multiple(() =>
        {
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { TokenizerFingerprint = "BBBB" }), Is.True, "Another tokenizer counts tokens differently, so the text is cut elsewhere.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { TokenizerPath = "/somewhere/else/tokenizer.json" }), Is.False, "It is the same tokenizer under another path.");
        });
    }

    [Test]
    public void ChangingTheChunkSettingsOfADataSourceDropsItsStoredIndex()
    {
        var embeddingProvider = StoredEmbeddingProvider();
        var stored = StoredDataSource();

        Assert.Multiple(() =>
        {
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { MaxChunkTokenLength = 256 }), Is.True, "Other chunk boundaries mean other vectors.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { ChunkOverlapTokenLength = 50 }), Is.True, "Another overlap changes what every chunk starts with.");
        });
    }

    /// <summary>
    /// Writing out what a data source already follows must not cost it its index.
    /// </summary>
    /// <remarks>
    /// A token limit of 0 means "follow the embedding provider", and opening the expert settings of a
    /// data source fills that empty field with exactly the provider's limit. Both are the same cut,
    /// so nobody may be asked about it -- and above all, nothing may be re-embedded for it. Somebody
    /// who only wanted to change how many matches an answer may use paid for a full rebuild.
    /// </remarks>
    [Test]
    public void SpellingOutWhatTheProviderAlreadyDictatesKeepsTheStoredIndex()
    {
        var embeddingProvider = StoredEmbeddingProvider() with { TokenLimit = 8192 };
        var followingTheProvider = StoredDataSource() with { MaxChunkTokenLength = 0 };

        Assert.Multiple(() =>
        {
            Assert.That(
                EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, followingTheProvider, followingTheProvider with { MaxChunkTokenLength = 8192 }),
                Is.False,
                "The provider limit typed into the field is the cut the data source already had.");

            Assert.That(
                EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, followingTheProvider, followingTheProvider with { MaxChunkTokenLength = 4096 }),
                Is.True,
                "Anything below the provider limit really does cut the text elsewhere.");
        });
    }

    /// <summary>
    /// An overlap larger than the chunk is capped, so several of them are the same cut.
    /// </summary>
    /// <remarks>
    /// The same reasoning as for the token limit: what counts is where the text is cut, not what
    /// somebody typed into the field.
    /// </remarks>
    [Test]
    public void AnOverlapWhichIsCappedAnywayKeepsTheStoredIndex()
    {
        var embeddingProvider = StoredEmbeddingProvider();
        var stored = StoredDataSource() with { MaxChunkTokenLength = 512, ChunkOverlapTokenLength = 600 };

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { ChunkOverlapTokenLength = 700 }),
            Is.False,
            "Both overlaps are capped to the chunk size, so the text is cut identically.");
    }

    [Test]
    public void HarmlessDataSourceEditsKeepTheStoredIndex()
    {
        var embeddingProvider = StoredEmbeddingProvider();
        var stored = StoredDataSource();

        Assert.Multiple(() =>
        {
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { Name = "Another name" }), Is.False, "The name is how the data source is offered, not how it was read.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { Description = "Another description" }), Is.False, "The description is there for the agent which picks data sources.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { MaxMatches = 42 }), Is.False, "How many matches an answer may use is decided per query.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(embeddingProvider, stored, stored with { ConfidenceLevel = ConfidenceLevel.HIGH }), Is.False, "The confidence level is enforced live on every request and changes no vector.");
        });
    }

    private static DataSourceLocalDirectory StoredDataSource() => new()
    {
        Num = 1,
        Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        Name = "Test data",
        Description = "Documents used by the tests.",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 100,
        ConfidenceLevel = ConfidenceLevel.LOW,
        Path = "/tmp/test-data",
    };

    private static EmbeddingProvider StoredEmbeddingProvider() =>
        new(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
}