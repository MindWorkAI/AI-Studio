using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Lua;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks which tokenizer the providers of a configuration plugin point to while the plugin starts.
/// </summary>
/// <remarks>
/// A configuration plugin names its tokenizers by paths inside the plugin. The settings point to the
/// copies the runtime stored of them instead, and an embedding provider keeps the fingerprint of its
/// copy, which is part of the embedding signature. Parsing the plugin replaced both with the path
/// from the plugin and an empty fingerprint, until the tokenizers were synchronized up to a second
/// later. An indexing run in between took that for another tokenizer and reset the index of every
/// data source of the provider, without asking.<br/><br/>
/// The settings are reached through Program.SERVICE_PROVIDER, which is why this fixture does not run
/// alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ManagedTokenizerStateTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("8d3e1c5a-6f2b-4a7d-9c1e-3b5f7a9d2e4c");

    private const string PROVIDER_ID = "1a7c3e5f-9b2d-4f6a-8c0e-2d4f6a8c0e1b";
    private const string EMBEDDING_PROVIDER_ID = "4e6a8c0e-2b4d-4f8a-9c1e-5f7a9b1d3e5f";
    private const string PLUGIN_PATH = "/plugins/configuration-of-the-organization";
    private const string CONFIGURED_TOKENIZER_PATH = "tokenizers/tokenizer.json";
    private const string STORED_CHAT_TOKENIZER_PATH = "/data/tokenizers/chat_" + PROVIDER_ID + "/tokenizer.json";
    private const string STORED_EMBEDDING_TOKENIZER_PATH = "/data/tokenizers/embedding_" + EMBEDDING_PROVIDER_ID + "/tokenizer.json";
    private const string STORED_FINGERPRINT = "AAAA";

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private SettingsManager settingsManager = null!;

    [SetUp]
    public void CreateEmptySettings()
    {
        // Only builds its HTTP clients. Nothing connects, because parsing a plugin does not reach the runtime:
        this.rustService = new RustService("1", "unused");
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).AddSingleton(this.rustService).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public async Task AnEmbeddingProviderKeepsItsStoredTokenizerWhileThePluginStarts()
    {
        await StartPluginAsync();
        this.StoreTokenizers();

        var configObjects = await StartPluginAsync();
        var embeddingProvider = this.settingsManager.ConfigurationData.EmbeddingProviders.Single();

        Assert.Multiple(() =>
        {
            Assert.That(embeddingProvider.TokenizerPath, Is.EqualTo(STORED_EMBEDDING_TOKENIZER_PATH), "Until the tokenizers are synchronized, the stored copy is the tokenizer this provider uses.");
            Assert.That(embeddingProvider.TokenizerFingerprint, Is.EqualTo(STORED_FINGERPRINT), "An empty fingerprint looks like another tokenizer and costs every data source of this provider its index.");
            Assert.That(configObjects.Single(x => x.Type == PluginConfigurationObjectType.EMBEDDING_PROVIDER).ConfiguredTokenizerPath, Is.EqualTo(CONFIGURED_TOKENIZER_PATH), "The synchronization learns from here which tokenizer the plugin wants.");
        });
    }

    [Test]
    public async Task TheEmbeddingSignatureStaysTheSameWhileThePluginStarts()
    {
        await StartPluginAsync();
        this.StoreTokenizers();
        var before = Signature(this.settingsManager.ConfigurationData.EmbeddingProviders.Single());

        await StartPluginAsync();
        var after = Signature(this.settingsManager.ConfigurationData.EmbeddingProviders.Single());

        Assert.That(after, Is.EqualTo(before), "An indexing run between the start of the plugin and the synchronization of its tokenizers compares exactly these two, and resets the index when they differ.");
    }

    [Test]
    public async Task AChatProviderKeepsItsStoredTokenizerWhileThePluginStarts()
    {
        await StartPluginAsync();
        this.StoreTokenizers();

        var configObjects = await StartPluginAsync();

        Assert.Multiple(() =>
        {
            Assert.That(this.settingsManager.ConfigurationData.Providers.Single().TokenizerPath, Is.EqualTo(STORED_CHAT_TOKENIZER_PATH), "The path inside the plugin is relative to the plugin, so no token could be counted with it.");
            Assert.That(configObjects.Single(x => x.Type == PluginConfigurationObjectType.LLM_PROVIDER).ConfiguredTokenizerPath, Is.EqualTo(CONFIGURED_TOKENIZER_PATH), "The synchronization learns from here which tokenizer the plugin wants.");
        });
    }

    [Test]
    public async Task ANewProviderStartsWithoutATokenizer()
    {
        var configObjects = await StartPluginAsync();
        var embeddingProvider = this.settingsManager.ConfigurationData.EmbeddingProviders.Single();

        Assert.Multiple(() =>
        {
            Assert.That(this.settingsManager.ConfigurationData.Providers.Single().TokenizerPath, Is.Empty, "Nothing is stored yet, and the path inside the plugin is no tokenizer the app could load.");
            Assert.That(embeddingProvider.TokenizerPath, Is.Empty);
            Assert.That(embeddingProvider.TokenizerFingerprint, Is.Empty);
            Assert.That(configObjects, Has.Count.EqualTo(2));
            Assert.That(configObjects.Select(x => x.ConfiguredTokenizerPath), Is.All.EqualTo(CONFIGURED_TOKENIZER_PATH), "The synchronization still learns which tokenizer to store.");
        });
    }

    /// <summary>
    /// Parses the providers of the plugin into the settings, as every start of the plugin does.
    /// </summary>
    /// <returns>The configuration objects the plugin defined.</returns>
    private static async Task<List<PluginConfigurationObject>> StartPluginAsync()
    {
        var state = LuaState.Create();
        await state.DoStringAsync($$"""
                                    CONFIG = {
                                        ["LLM_PROVIDERS"] = {
                                            {
                                                ["Id"] = "{{PROVIDER_ID}}",
                                                ["InstanceName"] = "Chat of the organization",
                                                ["UsedLLMProvider"] = "SELF_HOSTED",
                                                ["Host"] = "VLLM",
                                                ["Hostname"] = "https://llm.example.org",
                                                ["AdditionalJsonApiParameters"] = "",
                                                ["TokenizerPath"] = "{{CONFIGURED_TOKENIZER_PATH}}",
                                                ["Model"] = { ["Id"] = "chat-model", ["DisplayName"] = "Chat model" },
                                            },
                                        },
                                        ["EMBEDDING_PROVIDERS"] = {
                                            {
                                                ["Id"] = "{{EMBEDDING_PROVIDER_ID}}",
                                                ["Name"] = "Embeddings of the organization",
                                                ["UsedLLMProvider"] = "SELF_HOSTED",
                                                ["Host"] = "VLLM",
                                                ["Hostname"] = "https://embeddings.example.org",
                                                ["TokenizerPath"] = "{{CONFIGURED_TOKENIZER_PATH}}",
                                                ["Model"] = { ["Id"] = "embedding-model", ["DisplayName"] = "Embedding model" },
                                            },
                                        },
                                    }
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var mainTable))
            throw new InvalidOperationException("The configuration of this test is not a Lua table.");

        var configObjects = new List<PluginConfigurationObject>();
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, x => x.NextProviderNum, mainTable, PLUGIN_ID, ref configObjects, dryRun: false, PLUGIN_PATH);
        PluginConfigurationObject.TryParse(PluginConfigurationObjectType.EMBEDDING_PROVIDER, x => x.EmbeddingProviders, x => x.NextEmbeddingNum, mainTable, PLUGIN_ID, ref configObjects, dryRun: false, PLUGIN_PATH);
        return configObjects;
    }

    /// <summary>
    /// Points the providers to stored copies of their tokenizers, as the synchronization does after
    /// the plugin started.
    /// </summary>
    private void StoreTokenizers()
    {
        var data = this.settingsManager.ConfigurationData;
        data.Providers[0] = data.Providers[0] with { TokenizerPath = STORED_CHAT_TOKENIZER_PATH };
        data.EmbeddingProviders[0] = data.EmbeddingProviders[0] with { TokenizerPath = STORED_EMBEDDING_TOKENIZER_PATH, TokenizerFingerprint = STORED_FINGERPRINT };
    }

    private static string Signature(EmbeddingProvider embeddingProvider) => DataSourceEmbeddingService.BuildEmbeddingSignature(new DataSourceLocalDirectory
    {
        Num = 1,
        Id = "7b9d1f3a-5c7e-4a9b-8d2f-4a6c8e0b2d4f",
        Name = "Documents of the organization",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = embeddingProvider.Id,
        MaxChunkTokenLength = 512,
        Path = "/tmp/documents"
    }, embeddingProvider);
}