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

    /// <summary>
    /// A model ID which differs only in how it is written is a different model here.
    /// </summary>
    /// <remarks>
    /// This is why the embedding provider dialog adds a configured model to the list it loaded
    /// instead of matching it against that list. A server which writes the same model slightly
    /// differently -- with a tag where the user typed none, or in another case -- would otherwise
    /// have its spelling written into the settings on the next save, and every document of every
    /// data source behind that provider would be prepared again for a change nobody made.
    /// </remarks>
    [Test]
    public void AModelIdWhichOnlyReadsDifferentlyDropsTheStoredIndexAsWell()
    {
        var dataSource = StoredDataSource();
        var stored = StoredEmbeddingProvider() with { Model = new("nomic-embed-text", null) };

        Assert.Multiple(() =>
        {
            Assert.That(
                EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Model = new("nomic-embed-text:latest", null) }),
                Is.True,
                "The tag a server appends is part of the ID, and the ID is part of the signature.");

            Assert.That(
                EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Model = new("NOMIC-EMBED-TEXT", null) }),
                Is.True,
                "Compared ordinally, so another case is another model rather than the same one written louder.");

            Assert.That(
                EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { Model = new("nomic-embed-text", "Nomic Embed Text") }),
                Is.False,
                "The display name is decoration and reaches no vector, so loading the list may fill it in.");
        });
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
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { MaxChunkTokenLength = 256 }), Is.True, "Other chunk boundaries mean other vectors.");
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { ChunkOverlapTokenLength = 50 }), Is.True, "Another overlap changes what every chunk starts with.");
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
                EditKeepingTheProvider(embeddingProvider, followingTheProvider, followingTheProvider with { MaxChunkTokenLength = 8192 }),
                Is.False,
                "The provider limit typed into the field is the cut the data source already had.");

            Assert.That(
                EditKeepingTheProvider(embeddingProvider, followingTheProvider, followingTheProvider with { MaxChunkTokenLength = 4096 }),
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
            EditKeepingTheProvider(embeddingProvider, stored, stored with { ChunkOverlapTokenLength = 700 }),
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
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { Name = "Another name" }), Is.False, "The name is how the data source is offered, not how it was read.");
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { Description = "Another description" }), Is.False, "The description is there for the agent which picks data sources.");
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { MaxMatches = 42 }), Is.False, "How many matches an answer may use is decided per query.");
            Assert.That(EditKeepingTheProvider(embeddingProvider, stored, stored with { ConfidenceLevel = ConfidenceLevel.HIGH }), Is.False, "The confidence level is enforced live on every request and changes no vector.");
        });
    }

    /// <summary>
    /// Checks what changing the embedding of a data source costs.
    /// </summary>
    /// <remarks>
    /// This is why each side has to be asked with its own provider: a data source carries only the id
    /// of its embedding provider, and that id is nowhere in the signature. Asking both sides with the
    /// same provider would call this edit harmless, while the next indexing run throws everything away.
    /// </remarks>
    [Test]
    public void ChangingTheEmbeddingOfADataSourceDropsItsStoredIndex()
    {
        var storedProvider = StoredEmbeddingProvider();
        var anotherProvider = AnotherEmbeddingProvider();
        var stored = StoredDataSource();
        var moved = stored with { EmbeddingId = anotherProvider.Id };

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(stored, storedProvider, moved, anotherProvider),
            Is.True,
            "Another embedding provider means another vector space, so nothing stored survives it.");
    }

    /// <summary>
    /// Putting a data source back to work after its provider was deleted is a rebuild as well.
    /// </summary>
    /// <remarks>
    /// The provider a data source points at can be gone. What is stored was made by it, so pointing
    /// the source at any provider at all discards that -- and nobody may be surprised by it.
    /// </remarks>
    [Test]
    public void RepointingADataSourceWhoseProviderIsGoneDropsItsStoredIndex()
    {
        var stored = StoredDataSource();
        var anotherProvider = AnotherEmbeddingProvider();

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(stored, EmbeddingProvider.NONE, stored with { EmbeddingId = anotherProvider.Id }, anotherProvider),
            Is.True,
            "A provider which cannot be resolved stands in as NONE, which is a signature of its own.");
    }

    [Test]
    public void KeepingTheEmbeddingKeepsTheStoredIndex()
    {
        var storedProvider = StoredEmbeddingProvider();
        var stored = StoredDataSource();

        Assert.That(
            EmbeddingChangeImpact.AffectsStoredIndex(stored, storedProvider, stored with { Name = "Another name" }, storedProvider with { Name = "Renamed provider" }),
            Is.False,
            "Neither name reaches a vector, and the data source still points at the same provider.");
    }

    /// <summary>
    /// Asks the question for an edit which leaves the embedding provider of the data source alone.
    /// </summary>
    /// <param name="embeddingProvider">The provider both sides point at.</param>
    /// <param name="before">The data source as it is stored.</param>
    /// <param name="after">The data source as it would be stored.</param>
    /// <returns>True when the stored index would be discarded.</returns>
    private static bool EditKeepingTheProvider(EmbeddingProvider embeddingProvider, IDataSource before, IDataSource after) =>
        EmbeddingChangeImpact.AffectsStoredIndex(before, embeddingProvider, after, embeddingProvider);

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

    private static EmbeddingProvider AnotherEmbeddingProvider() =>
        new(2, "c1b5d5e3-2a4f-4b1b-9dae-6b7c8d9e0f12", "Other embeddings", LLMProviders.MISTRAL, new("mistral-embed", "mistral-embed"));
}