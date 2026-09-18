using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what makes the stored embeddings of a data source invalid.
/// </summary>
/// <remarks>
/// The embedding signature decides whether an index survives: when it differs from the one persisted
/// for a data source, everything stored is thrown away and embedded again. That is the right answer
/// for anything a vector depends on, and an expensive mistake for everything else. The confidence
/// level a data source asks of a provider used to be part of it, so changing that one setting
/// re-embedded every file of the source — at a cloud embedding provider, for real money and no gain.
/// </remarks>
[TestFixture]
public sealed class EmbeddingSignatureTests
{
    [Test]
    public void ChangingTheConfidenceLevelKeepsTheStoredEmbeddings()
    {
        var low = DataSource(ConfidenceLevel.LOW);
        var high = DataSource(ConfidenceLevel.HIGH);

        Assert.That(Signature(high), Is.EqualTo(Signature(low)), "The confidence level changes no vector, so the stored index stays valid and nothing is embedded again.");
    }

    [Test]
    public void ChangingTheChunkSizeDropsTheStoredEmbeddings()
    {
        var small = DataSource(ConfidenceLevel.LOW) with { MaxChunkTokenLength = 512 };
        var large = DataSource(ConfidenceLevel.LOW) with { MaxChunkTokenLength = 1024 };

        Assert.That(Signature(large), Is.Not.EqualTo(Signature(small)), "Other chunk boundaries mean other vectors, so the index has to be built again.");
    }

    [Test]
    public void ChangingTheEmbeddingModelDropsTheStoredEmbeddings()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);

        Assert.That(
            Signature(dataSource, EmbeddingProviderFor("text-embedding-3-large")),
            Is.Not.EqualTo(Signature(dataSource, EmbeddingProviderFor("text-embedding-3-small"))),
            "Another model means another vector space, so nothing stored may be kept.");
    }

    [Test]
    public void ChangingTheTokenizerContentDropsTheStoredEmbeddings()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var oneTokenizer = TokenizerAt("/data/tokenizers/embeddings/tokenizer.json", "AAAA");
        var anotherTokenizer = oneTokenizer with { TokenizerFingerprint = "BBBB" };

        Assert.That(
            Signature(dataSource, anotherTokenizer),
            Is.Not.EqualTo(Signature(dataSource, oneTokenizer)),
            "Another tokenizer cuts the text at other places. A tokenizer is stored under the name it came with, almost always tokenizer.json, so the path alone would not notice the swap.");
    }

    [Test]
    public void MovingTheTokenizerFileKeepsTheStoredEmbeddings()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var here = TokenizerAt("/data/tokenizers/embeddings/tokenizer.json", "AAAA");
        var there = here with { TokenizerPath = "/somewhere/else/tokenizers/embeddings/tokenizer.json" };

        Assert.That(
            Signature(dataSource, there),
            Is.EqualTo(Signature(dataSource, here)),
            "It is the same tokenizer and only the data directory moved, so embedding everything again would buy nothing.");
    }

    [Test]
    public void ChangingTheHuggingFaceInferenceProviderDropsTheStoredEmbeddings()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var oneBackend = EmbeddingProviderFor("text-embedding-3-small") with { HFInferenceProvider = HFInferenceProvider.GROQ };
        var anotherBackend = oneBackend with { HFInferenceProvider = HFInferenceProvider.CEREBRAS };

        Assert.That(
            Signature(dataSource, anotherBackend),
            Is.Not.EqualTo(Signature(dataSource, oneBackend)),
            "The same model name served by another backend is another vector source.");
    }

    [Test]
    public void TheSignatureOfAKnownConfigurationIsPinned()
    {
        Assert.That(
            Signature(DataSource(ConfidenceLevel.LOW)),
            Is.EqualTo("2|b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01|OPEN_AI|text-embedding-3-small|NONE|http://localhost:1234|NONE||8192|512|100|512|100"),
            "Reordering or extending the signature throws away every index anybody has. This test makes that a decision somebody takes rather than something which happens on the way past.");
    }

    private static string Signature(DataSourceLocalDirectory dataSource, EmbeddingProvider? embeddingProvider = null) =>
        DataSourceEmbeddingService.BuildEmbeddingSignature(
            dataSource,
            embeddingProvider ?? EmbeddingProviderFor("text-embedding-3-small"),
            new(512, 100));

    private static DataSourceLocalDirectory DataSource(ConfidenceLevel confidenceLevel) => new()
    {
        Num = 1,
        Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        Name = "Test data",
        Description = "Documents used by the tests.",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 100,
        ConfidenceLevel = confidenceLevel,
        Path = "/tmp/test-data",
    };

    private static EmbeddingProvider TokenizerAt(string tokenizerPath, string tokenizerFingerprint) =>
        EmbeddingProviderFor("text-embedding-3-small") with { TokenizerPath = tokenizerPath, TokenizerFingerprint = tokenizerFingerprint };

    private static EmbeddingProvider EmbeddingProviderFor(string modelId) =>
        new(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new(modelId, modelId));
}