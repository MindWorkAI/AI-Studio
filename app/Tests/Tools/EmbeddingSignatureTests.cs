using AIStudio.Provider;
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

    private static EmbeddingProvider EmbeddingProviderFor(string modelId) =>
        new(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new(modelId, modelId));
}