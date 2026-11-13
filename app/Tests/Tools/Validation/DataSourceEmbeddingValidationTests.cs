using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks the choice of the embedding provider in the dialogs of the local data sources.
/// </summary>
[TestFixture]
public sealed class DataSourceEmbeddingValidationTests
{
    private const string EMBEDDING_ID = "4b7c7f0e-5d1a-4c55-9a43-0f9b6f2c1d2e";

    [Test]
    public void AnEmptyChoiceIsRejected()
    {
        var validation = new DataSourceValidation { GetSelectedEmbeddingProvider = () => null };

        Assert.That(validation.ValidateEmbeddingProviderAccess(string.Empty), Is.Not.Null);
    }

    [Test]
    public void AProviderWhichNoLongerExistsIsRejected()
    {
        // The dialogs resolve the id against the configured providers, so a deleted one resolves to nothing:
        var validation = new DataSourceValidation { GetSelectedEmbeddingProvider = () => null };

        Assert.That(validation.ValidateEmbeddingProviderAccess(EMBEDDING_ID), Is.Not.Null);
    }

    [Test]
    public void AnExistingProviderIsAccepted()
    {
        var embeddingProvider = new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
        var validation = new DataSourceValidation { GetSelectedEmbeddingProvider = () => embeddingProvider };

        Assert.That(validation.ValidateEmbeddingProviderAccess(EMBEDDING_ID), Is.Null);
    }
}