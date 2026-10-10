using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks what the synchronization asks the runtime to do with the tokenizer a configuration plugin
/// names for a provider.
/// </summary>
/// <remarks>
/// Whenever a configuration plugin starts, each of its providers gets the tokenizer the plugin names:
/// the runtime checks the file and stores a copy below the data directory, which the provider then
/// points to. When the plugin names none, or one which is unusable or lies outside the plugin, the
/// stored copy is deleted instead. These tests stand in for the runtime, so they see every call the
/// synchronization makes and in which order.
/// </remarks>
[TestFixture]
public sealed class TokenizerSyncTests
{
    private const string CONFIGURED_TOKENIZER_PATH = "tokenizers/tokenizer.json";
    private const string LOG_NAME = "embedding provider 'Embeddings of the organization'";

    private static readonly string MODEL_ID = TokenizerModelId.ForEmbeddingProviderId("4e6a8c0e-2b4d-4f8a-9c1e-5f7a9b1d3e5f");

    private string testDirectory = string.Empty;
    private string pluginDirectory = string.Empty;
    private string dataDirectory = string.Empty;
    private string sourcePath = string.Empty;

    [SetUp]
    public void CreatePluginWithATokenizer()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-tokenizers-{Guid.NewGuid():N}");
        this.pluginDirectory = Path.Combine(this.testDirectory, "plugin");
        this.dataDirectory = Path.Combine(this.testDirectory, "data");
        this.sourcePath = Path.GetFullPath(Path.Combine(this.pluginDirectory, CONFIGURED_TOKENIZER_PATH));

        Directory.CreateDirectory(Path.GetDirectoryName(this.sourcePath)!);
        File.WriteAllText(this.sourcePath, """{ "version": "1.0", "model": { "type": "WordPiece" } }""");
    }

    [TearDown]
    public void RemoveTestDirectory()
    {
        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task AValidTokenizerIsCheckedAndStored()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var storedPath = await PluginConfigurationObject.SyncTokenizerAsync(storage, CONFIGURED_TOKENIZER_PATH, this.pluginDirectory, MODEL_ID, LOG_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }));
            Assert.That(storedPath, Is.Not.Empty.And.Not.EqualTo(this.sourcePath), "The provider points to the copy, not to the file inside the plugin.");
            Assert.That(File.ReadAllBytes(storedPath), Is.EqualTo(File.ReadAllBytes(this.sourcePath)));
        });
    }

    [Test]
    public async Task AnInvalidTokenizerDeletesTheStoredOne()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory) { AcceptsTokenizers = false };

        var storedPath = await PluginConfigurationObject.SyncTokenizerAsync(storage, CONFIGURED_TOKENIZER_PATH, this.pluginDirectory, MODEL_ID, LOG_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"delete {MODEL_ID}" }), "A tokenizer stored earlier must not outlive the plugin naming an unusable one.");
            Assert.That(storedPath, Is.Empty);
        });
    }

    [Test]
    public async Task NoTokenizerDeletesTheStoredOne()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var storedPath = await PluginConfigurationObject.SyncTokenizerAsync(storage, string.Empty, this.pluginDirectory, MODEL_ID, LOG_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"delete {MODEL_ID}" }), "The plugin no longer names a tokenizer, so the one stored for it goes.");
            Assert.That(storedPath, Is.Empty);
        });
    }

    [Test]
    public async Task ATokenizerOutsideThePluginIsNeitherReadNorStored()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var storedPath = await PluginConfigurationObject.SyncTokenizerAsync(storage, "../outside/tokenizer.json", this.pluginDirectory, MODEL_ID, LOG_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"delete {MODEL_ID}" }), "A plugin must not make the app read files from anywhere else on the machine.");
            Assert.That(storedPath, Is.Empty);
        });
    }

    [Test]
    public async Task ATokenizerWhichCannotBeStoredLeavesTheProviderWithoutOne()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory) { StoresTokenizers = false };

        var storedPath = await PluginConfigurationObject.SyncTokenizerAsync(storage, CONFIGURED_TOKENIZER_PATH, this.pluginDirectory, MODEL_ID, LOG_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }));
            Assert.That(storedPath, Is.Empty);
        });
    }

    /// <summary>
    /// Stands in for the runtime: writes down every call, and stores a tokenizer the way the runtime
    /// does, as a copy in a directory of its model below the data directory.
    /// </summary>
    private sealed class RecordingTokenizerStorage(string dataDirectory) : ITokenizerStorage
    {
        /// <summary>
        /// The calls so far, each as its name and arguments.
        /// </summary>
        public List<string> Calls { get; } = [];

        /// <summary>
        /// Whether every file passes the check.
        /// </summary>
        public bool AcceptsTokenizers { get; init; } = true;

        /// <summary>
        /// Whether storing works.
        /// </summary>
        public bool StoresTokenizers { get; init; } = true;

        public Task<TokenizerResponse> ValidateTokenizer(string filePath)
        {
            this.Calls.Add($"validate {filePath}");
            return Task.FromResult(this.AcceptsTokenizers
                ? new TokenizerResponse(true, 0, string.Empty)
                : new TokenizerResponse(false, 0, "The file is not a tokenizer."));
        }

        public Task<TokenizerResponse> StoreTokenizer(string modelId, string filePath)
        {
            this.Calls.Add($"store {modelId} {filePath}");
            if (!this.StoresTokenizers)
                return Task.FromResult(new TokenizerResponse(false, 0, "The disk is full."));

            var modelDirectory = this.DeleteModelDirectory(modelId);
            Directory.CreateDirectory(modelDirectory);

            var storedPath = Path.Combine(modelDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, storedPath);
            return Task.FromResult(new TokenizerResponse(true, 0, string.Empty, storedPath));
        }

        public Task<TokenizerResponse> DeleteTokenizer(string modelId)
        {
            this.Calls.Add($"delete {modelId}");
            this.DeleteModelDirectory(modelId);
            return Task.FromResult(new TokenizerResponse(true, 0, string.Empty));
        }

        private string DeleteModelDirectory(string modelId)
        {
            var modelDirectory = Path.Combine(dataDirectory, "tokenizers", modelId);
            if (Directory.Exists(modelDirectory))
                Directory.Delete(modelDirectory, true);

            return modelDirectory;
        }
    }
}