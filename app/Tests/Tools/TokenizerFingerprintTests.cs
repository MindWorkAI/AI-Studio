using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which fingerprint the embedding provider dialog stores along with a tokenizer.
/// </summary>
/// <remarks>
/// The fingerprint enters the embedding signature. Storing a provider reads one only for a tokenizer
/// which arrived without it, such as the tokenizer of an imported provider. A provider which has one
/// keeps it: reading the file again would cost time for nothing, and any other answer would throw away
/// the index of every data source behind that provider.
/// </remarks>
[TestFixture]
public sealed class TokenizerFingerprintTests
{
    private const string EMBEDDING_ID = "7d2f9b1c-4e6a-4c8d-a1f3-5b9e0c2d4f6a";

    private const string STORED_FINGERPRINT = "AAAA";

    private string tokenizerPath = string.Empty;

    [SetUp]
    public void CreateTokenizer()
    {
        this.tokenizerPath = Path.Combine(Path.GetTempPath(), $"ai-studio-tokenizer-{Guid.NewGuid():N}.json");
        File.WriteAllText(this.tokenizerPath, """{ "version": "1.0", "model": { "type": "WordPiece" } }""");
    }

    [TearDown]
    public void RemoveTokenizer() => File.Delete(this.tokenizerPath);

    [Test]
    public async Task AStoredFingerprintIsKeptWithoutReadingTheTokenizer()
    {
        var dataSource = new DataSourceLocalDirectory { Id = Guid.NewGuid().ToString(), Name = "Reports", Type = DataSourceType.LOCAL_DIRECTORY, EmbeddingId = EMBEDDING_ID };
        var stored = new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"), TokenizerPath: this.tokenizerPath, TokenizerFingerprint: STORED_FINGERPRINT);
        var storedFingerprint = await TokenizerFingerprint.KeepOrReadAsync(stored.TokenizerPath, stored.TokenizerFingerprint);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(storedFingerprint, Is.EqualTo(STORED_FINGERPRINT), "The file was read although its fingerprint was known.");
            Assert.That(EmbeddingChangeImpact.AffectsStoredIndex(dataSource, stored, stored with { TokenizerFingerprint = storedFingerprint }), Is.False, "Saving a provider without touching its tokenizer must not index its data sources again.");
        }
    }

    [Test]
    public async Task WithoutATokenizerThereIsNoFingerprint() =>
        Assert.That(await TokenizerFingerprint.KeepOrReadAsync(string.Empty, STORED_FINGERPRINT), Is.Empty, "A fingerprint left over from a removed tokenizer would make the signature claim a tokenizer.");
}