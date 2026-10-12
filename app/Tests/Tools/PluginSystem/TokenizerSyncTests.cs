using AIStudio.Tools;
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
/// stored copy is deleted instead. Checking takes most of a second for a common tokenizer, so a copy
/// with the same content as the file in the plugin is kept as it is. These tests stand in for the
/// runtime, so they see every call the synchronization makes and in which order.
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

        var (storedPath, fingerprint) = await this.SyncAsync(storage, string.Empty);
        var fingerprintOfTheCopy = await TokenizerFingerprint.ForFileAsync(storedPath);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }), "Without a stored copy, there is nothing to compare with.");
            Assert.That(storedPath, Is.Not.Empty.And.Not.EqualTo(this.sourcePath), "The provider points to the copy, not to the file inside the plugin.");
            Assert.That(File.ReadAllBytes(storedPath), Is.EqualTo(File.ReadAllBytes(this.sourcePath)));
            Assert.That(fingerprint, Is.Not.Empty.And.EqualTo(fingerprintOfTheCopy));
        });
    }

    [Test]
    public async Task AnUnchangedTokenizerIsNeitherCheckedNorStoredAgain()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var secondStart = await this.SyncAsync(storage, firstStart.Path);
        var fingerprintOfTheCopy = await TokenizerFingerprint.ForFileAsync(firstStart.Path);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.Empty, "Checking the same file again would cost most of a second and change nothing.");
            Assert.That(secondStart, Is.EqualTo(firstStart), "A fingerprint which differs from the one before would cost every data source of the provider its index.");
            Assert.That(secondStart.Fingerprint, Is.EqualTo(fingerprintOfTheCopy));
        });
    }

    [Test]
    public async Task AMissingCopyIsStoredAgain()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        File.Delete(firstStart.Path);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var secondStart = await this.SyncAsync(storage, firstStart.Path);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }));
            Assert.That(File.Exists(secondStart.Path), Is.True);
            Assert.That(secondStart.Fingerprint, Is.EqualTo(firstStart.Fingerprint), "It is the same tokenizer, so the index stays.");
        });
    }

    [Test]
    public async Task AChangedCopyIsStoredAgainEvenAtTheSameSize()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        var copy = File.ReadAllBytes(firstStart.Path);
        copy[^3] ^= 0x01;
        File.WriteAllBytes(firstStart.Path, copy);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var secondStart = await this.SyncAsync(storage, firstStart.Path);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }), "Size and time do not tell a damaged copy apart, the content does.");
            Assert.That(File.ReadAllBytes(secondStart.Path), Is.EqualTo(File.ReadAllBytes(this.sourcePath)));
            Assert.That(secondStart.Fingerprint, Is.EqualTo(firstStart.Fingerprint));
        });
    }

    [Test]
    public async Task AChangedTokenizerInThePluginIsCheckedAndStored()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        File.WriteAllText(this.sourcePath, """{ "version": "1.0", "model": { "type": "BPE" } }""");
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var secondStart = await this.SyncAsync(storage, firstStart.Path);
        var fingerprintOfThePluginFile = await TokenizerFingerprint.ForFileAsync(this.sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }));
            Assert.That(secondStart.Fingerprint, Is.Not.EqualTo(firstStart.Fingerprint), "Another tokenizer cuts the text at other places, so the index has to go.");
            Assert.That(secondStart.Fingerprint, Is.EqualTo(fingerprintOfThePluginFile));
        });
    }

    [Test]
    public async Task TwoMissingFilesAreNotTheSameTokenizer()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        File.Delete(firstStart.Path);
        File.Delete(this.sourcePath);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var secondStart = await this.SyncAsync(storage, firstStart.Path);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"delete {MODEL_ID}" }), "Neither file can be read, so nothing says they hold the same tokenizer.");
            Assert.That(secondStart.Path, Is.Empty);
            Assert.That(secondStart.Fingerprint, Is.Empty);
        });
    }

    [Test]
    public async Task AnUnreadableCopyTakesTheFingerprintOfThePluginFile()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory) { MakesUnreadableCopies = true };

        var (storedPath, fingerprint) = await this.SyncAsync(storage, string.Empty);
        var fingerprintOfThePluginFile = await TokenizerFingerprint.ForFileAsync(this.sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(storedPath, Is.Not.Empty);
            Assert.That(File.Exists(storedPath), Is.False, "Otherwise this test checks nothing.");
            Assert.That(fingerprint, Is.Not.Empty.And.EqualTo(fingerprintOfThePluginFile), "The runtime copies the file as it is, so the file in the plugin tells what the copy holds.");
        });
    }

    [Test]
    public async Task AnInvalidTokenizerDeletesTheStoredOne()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory) { AcceptsTokenizers = false };

        var (storedPath, fingerprint) = await this.SyncAsync(storage, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"delete {MODEL_ID}" }), "A tokenizer stored earlier must not outlive the plugin naming an unusable one.");
            Assert.That(storedPath, Is.Empty);
            Assert.That(fingerprint, Is.Empty);
        });
    }

    [Test]
    public async Task NoTokenizerDeletesTheStoredOne()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var (storedPath, fingerprint) = await this.SyncAsync(storage, firstStart.Path, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"delete {MODEL_ID}" }), "The plugin no longer names a tokenizer, so the one stored for it goes.");
            Assert.That(File.Exists(firstStart.Path), Is.False);
            Assert.That(storedPath, Is.Empty);
            Assert.That(fingerprint, Is.Empty);
        });
    }

    [Test]
    public async Task ATokenizerOutsideThePluginIsNeitherReadNorStored()
    {
        var firstStart = await this.SyncAsync(new RecordingTokenizerStorage(this.dataDirectory), string.Empty);
        var storage = new RecordingTokenizerStorage(this.dataDirectory);

        var (storedPath, fingerprint) = await this.SyncAsync(storage, firstStart.Path, "../outside/tokenizer.json");

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"delete {MODEL_ID}" }), "A plugin must not make the app read files from anywhere else on the machine.");
            Assert.That(storedPath, Is.Empty);
            Assert.That(fingerprint, Is.Empty);
        });
    }

    [Test]
    public async Task ATokenizerWhichCannotBeStoredLeavesTheProviderWithoutOne()
    {
        var storage = new RecordingTokenizerStorage(this.dataDirectory) { StoresTokenizers = false };

        var (storedPath, fingerprint) = await this.SyncAsync(storage, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(storage.Calls, Is.EqualTo(new[] { $"validate {this.sourcePath}", $"store {MODEL_ID} {this.sourcePath}" }));
            Assert.That(storedPath, Is.Empty);
            Assert.That(fingerprint, Is.Empty);
        });
    }

    /// <summary>
    /// Synchronizes the tokenizer of the provider, as one start of the plugin does.
    /// </summary>
    /// <param name="storage">The stand-in for the runtime.</param>
    /// <param name="storedTokenizerPath">The copy the provider points to so far, or an empty string.</param>
    /// <param name="configuredTokenizerPath">The tokenizer path as the plugin names it.</param>
    /// <returns>The copy the provider points to afterward, and its fingerprint.</returns>
    private Task<StoredTokenizer> SyncAsync(RecordingTokenizerStorage storage, string storedTokenizerPath, string configuredTokenizerPath = CONFIGURED_TOKENIZER_PATH) =>
        PluginConfigurationObject.SyncTokenizerAsync(storage, configuredTokenizerPath, storedTokenizerPath, this.pluginDirectory, MODEL_ID, LOG_NAME);

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
        /// Whether every existing file passes the check. A missing one never does.
        /// </summary>
        public bool AcceptsTokenizers { get; init; } = true;

        /// <summary>
        /// Whether storing works.
        /// </summary>
        public bool StoresTokenizers { get; init; } = true;

        /// <summary>
        /// Whether storing reports a copy which cannot be read afterward.
        /// </summary>
        public bool MakesUnreadableCopies { get; init; }

        public Task<TokenizerResponse> ValidateTokenizer(string filePath)
        {
            this.Calls.Add($"validate {filePath}");
            return Task.FromResult(this.AcceptsTokenizers && File.Exists(filePath)
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
            if (!this.MakesUnreadableCopies)
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