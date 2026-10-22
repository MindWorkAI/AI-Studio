using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Lua;
using Lua.Standard;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the tokenizer path which an exported provider carries, as a configuration plugin reads it back.
/// </summary>
/// <remarks>
/// In memory, a provider points to its tokenizer copy below the data directory by an absolute path.
/// Exported as it is, that path named the user name of the administrator, broke the Lua code on Windows,
/// and was ignored by every configuration plugin, which only reads tokenizers inside its own folder.
/// The export names the copy relative to the data directory instead, which is also where the
/// administrator finds the file to copy into the plugin.
/// </remarks>
[TestFixture]
public sealed class ProviderLuaExportTests
{
    private const string PROVIDER_ID = "5f3c2a1e-7b4d-4e8f-9a6c-1d2e3f4a5b6c";

    private static readonly Guid PLUGIN_ID = new("33333333-3333-3333-3333-333333333333");

    private string testDirectory = string.Empty;

    [SetUp]
    public void CreateTestDirectory() => this.testDirectory = Directory.CreateTempSubdirectory("provider-lua-export-").FullName;

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

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public async Task AWindowsPathLeavesNeitherTheUserNameNorTheDriveBehind(PluginConfigurationObjectType type)
    {
        var modelId = ModelIdOf(type);
        var (luaCode, readBack) = await ExportAndReadBackAsync(type, $@"C:\Users\Jane Doe\AppData\Roaming\com.github.mindwork-ai.ai-studio\data\tokenizers\{modelId}\tokenizer.json");

        Assert.Multiple(() =>
        {
            Assert.That(readBack, Is.EqualTo($"tokenizers/{modelId}/tokenizer.json"), "A configuration plugin reads the path relative to its own folder.");
            Assert.That(luaCode, Does.Not.Contain("Jane"));
            Assert.That(luaCode, Does.Not.Contain("C:"));
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public async Task AQuoteInTheFileNameSurvivesTheRoundTrip(PluginConfigurationObjectType type)
    {
        var modelId = ModelIdOf(type);
        var (_, readBack) = await ExportAndReadBackAsync(type, $"""/Users/jane/Library/Application Support/com.github.mindwork-ai.ai-studio/data/tokenizers/{modelId}/my "best" tokenizer.json""");

        Assert.That(readBack, Is.EqualTo($"""tokenizers/{modelId}/my "best" tokenizer.json"""));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public async Task AProviderWithoutATokenizerExportsAnEmptyPathAndNoNote(PluginConfigurationObjectType type)
    {
        var (luaCode, readBack) = await ExportAndReadBackAsync(type, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(readBack, Is.Empty);
            Assert.That(luaCode, Does.Contain("""["TokenizerPath"] = "",""").And.Not.Contain("tokenizer of its own"));
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public async Task AConfigurationPluginFindsTheTokenizerWhereTheExportSaysItBelongs(PluginConfigurationObjectType type)
    {
        var modelId = ModelIdOf(type);
        var dataDirectory = Path.Combine(this.testDirectory, "data");
        var pluginDirectory = Path.Combine(this.testDirectory, "plugin");
        var copyInTheDataDirectory = CreateTokenizer(Path.Combine(dataDirectory, "tokenizers", modelId, "tokenizer.json"));

        var (_, exportedPath) = await ExportAndReadBackAsync(type, copyInTheDataDirectory);
        var fileInThePlugin = CreateTokenizer(Path.Combine(pluginDirectory, exportedPath));

        var storage = new AcceptingTokenizerStorage();
        var synced = await PluginConfigurationObject.SyncTokenizerAsync(storage, exportedPath, string.Empty, pluginDirectory, modelId, "the exported provider");
        var syncedWithTheAbsolutePath = await PluginConfigurationObject.SyncTokenizerAsync(storage, copyInTheDataDirectory, string.Empty, pluginDirectory, modelId, "the exported provider");

        Assert.Multiple(() =>
        {
            Assert.That(synced.Path, Is.EqualTo(fileInThePlugin), "The administrator copies the file to the same place in the plugin, where the plugin looks for it.");
            Assert.That(syncedWithTheAbsolutePath, Is.EqualTo(StoredTokenizer.NONE), "What the export wrote before lies outside of the plugin, so the plugin ignored it.");
        });
    }

    private static string ModelIdOf(PluginConfigurationObjectType type) => type switch
    {
        PluginConfigurationObjectType.LLM_PROVIDER => TokenizerModelId.ForProviderId(PROVIDER_ID),
        PluginConfigurationObjectType.EMBEDDING_PROVIDER => TokenizerModelId.ForEmbeddingProviderId(PROVIDER_ID),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This type has no tokenizer."),
    };

    /// <summary>
    /// Exports a provider with the given tokenizer path and reads the export back, as a configuration plugin does.
    /// </summary>
    /// <returns>The exported Lua code and the tokenizer path the plugin reads from it.</returns>
    private static async Task<(string LuaCode, string TokenizerPath)> ExportAndReadBackAsync(PluginConfigurationObjectType type, string tokenizerPath)
    {
        var luaCode = type switch
        {
            PluginConfigurationObjectType.LLM_PROVIDER => new AIStudio.Settings.Provider(1, PROVIDER_ID, "Chat provider", LLMProviders.OPEN_AI, new("gpt-6", "GPT-6"), TokenizerPath: tokenizerPath).ExportAsConfigurationSection(),
            PluginConfigurationObjectType.EMBEDDING_PROVIDER => new EmbeddingProvider(1, PROVIDER_ID, "Embedding provider", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"), TokenizerPath: tokenizerPath).ExportAsConfigurationSection(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This type has no tokenizer."),
        };

        var section = PluginConfigurationObjectDescriptors.Get(type).LuaTableName;
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    CONFIG = {}
                                    CONFIG["{{section}}"] = {}
                                    {{luaCode}}
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var configTable) ||
            !configTable[section].TryRead<LuaTable>(out var providersTable) ||
            !providersTable[1].TryRead<LuaTable>(out var providerTable))
            throw new InvalidOperationException("The export did not produce a provider table.");

        return type switch
        {
            PluginConfigurationObjectType.LLM_PROVIDER when AIStudio.Settings.Provider.TryParseProviderTable(1, providerTable, PLUGIN_ID, string.Empty, dryRun: true, out var provider) => (luaCode, ((AIStudio.Settings.Provider)provider).TokenizerPath),
            PluginConfigurationObjectType.EMBEDDING_PROVIDER when EmbeddingProvider.TryParseEmbeddingProviderTable(1, providerTable, PLUGIN_ID, string.Empty, dryRun: true, out var provider) => (luaCode, ((EmbeddingProvider)provider).TokenizerPath),
            _ => throw new InvalidOperationException("The exported provider could not be read."),
        };
    }

    private static string CreateTokenizer(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{ "version": "1.0", "model": { "type": "WordPiece" } }""");
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Stands in for the runtime: every existing file passes as a tokenizer, and storing one leaves it where it is.
    /// </summary>
    private sealed class AcceptingTokenizerStorage : ITokenizerStorage
    {
        public Task<TokenizerResponse> ValidateTokenizer(string filePath) => Task.FromResult(new TokenizerResponse(File.Exists(filePath), 0, string.Empty));

        public Task<TokenizerResponse> StoreTokenizer(string modelId, string filePath) => Task.FromResult(new TokenizerResponse(true, 0, string.Empty, filePath));

        public Task<TokenizerResponse> DeleteTokenizer(string modelId) => Task.FromResult(new TokenizerResponse(true, 0, string.Empty));
    }
}