using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the providers an organization prohibited through a configuration plugin.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed
/// setting writes its value and its lock there. Names are written out in full where a short one
/// would find a namespace of this assembly first, see PreselectedProviderTests.
/// </remarks>
[TestFixture]
public sealed class ProhibitedProvidersTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("7b0f9d4e-6a1c-4c55-9a33-0f6d2c9e8b41");
    private const string ALLOWED_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string PROHIBITED_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Creating the settings data registers every managed setting:
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        // The managed state of a setting outlives this fixture, so we release what the plugin took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task APluginProhibitsTheProvidersItNames()
    {
        await ProcessAsync("""{ "DEEP_SEEK", "mistral" }""");

        Assert.Multiple(() =>
        {
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.DEEP_SEEK, LLMProviders.MISTRAL }), "Names count whatever their case, as everywhere in a configuration.");
            Assert.That(this.settingsManager.IsProviderProhibited(LLMProviders.DEEP_SEEK), Is.True);
            Assert.That(this.settingsManager.IsProviderProhibited(LLMProviders.OPEN_AI), Is.False, "A provider the plugin does not name stays allowed.");
        });
    }

    /// <summary>
    /// Checks that a name which is no provider prohibits nothing and leaves the rest of the list intact.
    /// </summary>
    /// <remarks>
    /// "DEEPSEEK" is the typo an administrator would make. It must not take the valid names down with it,
    /// and it must not turn into some other provider either.
    /// </remarks>
    [Test]
    public async Task AnUnknownNameProhibitsNothing()
    {
        await ProcessAsync("""{ "DEEPSEEK", "ANTHROPIC", "11" }""");

        Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.ANTHROPIC }), "Only the valid name may count, and a number is no name.");
    }

    [Test]
    public async Task TheProhibitionIsLocked()
    {
        await ProcessAsync("""{ "DEEP_SEEK" }""");

        var isManaged = ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.True, "Users must not lift a prohibition of their organization.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
        });
    }

    [Test]
    public async Task RemovingThePluginLiftsTheProhibition()
    {
        await ProcessAsync("""{ "DEEP_SEEK" }""");

        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Assert.That(this.settingsManager.IsProviderProhibited(LLMProviders.DEEP_SEEK), Is.False, "Without its plugin, the prohibition has to go, and the providers set up before are usable again.");
    }

    /// <summary>
    /// Checks that NONE is never prohibited.
    /// </summary>
    /// <remarks>
    /// NONE stands for "nothing selected". Counting it as prohibited would show the notice of the
    /// organization in every empty provider dialog.
    /// </remarks>
    [Test]
    public async Task NoneIsNeverProhibited()
    {
        await ProcessAsync("""{ "NONE" }""");

        Assert.That(this.settingsManager.IsProviderProhibited(LLMProviders.NONE), Is.False);
    }

    [Test]
    public void SelectionsLeaveOutProhibitedProviders()
    {
        this.AddProvider(ALLOWED_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(PROHIBITED_PROVIDER_ID, LLMProviders.DEEP_SEEK);
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.DEEP_SEEK);

        var selectable = this.settingsManager.GetConfidentProviders(AIStudio.Tools.Components.CHAT).Select(x => x.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(selectable, Is.EqualTo(new[] { ALLOWED_PROVIDER_ID }), "A prohibited provider must not be offered, whatever its confidence level.");
            Assert.That(this.settingsManager.GetAllProviders(), Has.Count.EqualTo(2), "The settings still list it, so that the user can see and delete it.");
        });
    }

    [Test]
    public void ThePreselectionSkipsProhibitedProviders()
    {
        this.AddProvider(ALLOWED_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(PROHIBITED_PROVIDER_ID, LLMProviders.DEEP_SEEK);
        this.settingsManager.ConfigurationData.App.PreselectedProvider = PROHIBITED_PROVIDER_ID;
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.DEEP_SEEK);

        Assert.Multiple(() =>
        {
            Assert.That(this.settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.CHAT, PROHIBITED_PROVIDER_ID), Is.EqualTo(AIStudio.Settings.Provider.NONE), "Neither the current provider nor the app-wide default may hand over a prohibited provider.");
            Assert.That(this.settingsManager.GetChatProviderForLoadedChat(PROHIBITED_PROVIDER_ID).Id, Is.Not.EqualTo(PROHIBITED_PROVIDER_ID), "A chat stored with a prohibited provider has to continue with another one.");
        });
    }

    /// <summary>
    /// Checks that a single prohibited provider is not handed over by the shortcut for a single provider.
    /// </summary>
    /// <remarks>
    /// With exactly one configured provider, GetPreselectedProvider returns it without looking at any
    /// preselection. That shortcut must check the prohibition as well.
    /// </remarks>
    [Test]
    public void TheOnlyProviderIsNotHandedOverWhenProhibited()
    {
        this.AddProvider(PROHIBITED_PROVIDER_ID, LLMProviders.DEEP_SEEK);
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.DEEP_SEEK);

        Assert.That(this.settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.CHAT), Is.EqualTo(AIStudio.Settings.Provider.NONE));
    }

    [Test]
    public void AProhibitedProviderIsAllowedInNoChat()
    {
        this.AddProvider(ALLOWED_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(PROHIBITED_PROVIDER_ID, LLMProviders.DEEP_SEEK);
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.DEEP_SEEK);

        var allowed = this.settingsManager.GetProviderById(ALLOWED_PROVIDER_ID);
        var prohibited = this.settingsManager.GetProviderById(PROHIBITED_PROVIDER_ID);
        ChatThread? newChat = null;

        Assert.Multiple(() =>
        {
            Assert.That(newChat.IsLLMProviderAllowed(prohibited), Is.False, "Even a new chat, which has no requirements yet, must not use a prohibited provider.");
            Assert.That(new ChatThread().IsLLMProviderAllowed(prohibited), Is.False);
            Assert.That(newChat.IsLLMProviderAllowed(allowed), Is.True);
        });
    }

    [Test]
    public void ADataSourceEmbedsNothingWithAProhibitedProvider()
    {
        var embeddingProvider = this.AddEmbeddingProvider(LLMProviders.MISTRAL);
        var dataSource = new DataSourceLocalDirectory { EmbeddingId = embeddingProvider.Id };
        Assert.That(DataSourceEmbeddingProviders.TryResolve(this.settingsManager, dataSource, out _), Is.True, "Before the prohibition, the provider is there to use.");

        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.MISTRAL);

        Assert.Multiple(() =>
        {
            Assert.That(DataSourceEmbeddingProviders.TryResolve(this.settingsManager, dataSource, out _), Is.False, "Indexing and searching must not embed anything with a prohibited provider.");
            Assert.That(DataSourceEmbeddingProviders.IsProhibited(this.settingsManager, dataSource), Is.True, "The status of the data source has to name the prohibition as the reason.");
        });
    }

    [Test]
    public void TheDataSourceDialogsRefuseAProhibitedEmbeddingProvider()
    {
        var embeddingProvider = this.AddEmbeddingProvider(LLMProviders.MISTRAL);
        var validation = new DataSourceValidation
        {
            GetSelectedEmbeddingProvider = () => embeddingProvider,
            GetSettingsManager = () => this.settingsManager,
        };

        Assert.That(validation.ValidateEmbeddingProviderAccess(embeddingProvider.Id), Is.Null, "Before the prohibition, the provider may be selected.");

        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.MISTRAL);

        Assert.That(validation.ValidateEmbeddingProviderAccess(embeddingProvider.Id), Is.Not.Null, "A data source saved with a prohibited provider would never be embedded.");
    }

    [Test]
    public void AProhibitedTranscriptionProviderTranscribesNothing()
    {
        var transcriptionProvider = new TranscriptionProvider { Id = Guid.NewGuid().ToString(), UsedLLMProvider = LLMProviders.OPEN_AI };
        this.settingsManager.ConfigurationData.TranscriptionProviders.Add(transcriptionProvider);
        this.settingsManager.ConfigurationData.App.UseTranscriptionProvider = transcriptionProvider.Id;

        // GetUsableProvider reads only the settings, so the other services stay out of this test:
        using var transcriptionService = new MediaTranscriptionService(null!, this.settingsManager, null!, [], NullLogger<MediaTranscriptionService>.Instance);
        Assert.That(transcriptionService.GetUsableProvider(), Is.Not.Null, "Before the prohibition, the provider is there to use.");

        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.OPEN_AI);

        Assert.That(transcriptionService.GetUsableProvider(), Is.Null, "Dictation and the transcription of recordings must not reach a prohibited provider.");
    }

    /// <summary>
    /// Adds an embedding provider to the settings.
    /// </summary>
    private EmbeddingProvider AddEmbeddingProvider(LLMProviders llmProvider)
    {
        var embeddingProviders = this.settingsManager.ConfigurationData.EmbeddingProviders;
        var embeddingProvider = new EmbeddingProvider((uint)embeddingProviders.Count + 1, Guid.NewGuid().ToString(), $"Embedding {embeddingProviders.Count + 1}", llmProvider, new("test-embedding", null));
        embeddingProviders.Add(embeddingProvider);
        return embeddingProvider;
    }

    /// <summary>
    /// Adds a provider to the settings.
    /// </summary>
    private void AddProvider(string id, LLMProviders llmProvider)
    {
        var providers = this.settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null)));
    }

    /// <summary>
    /// Lets a configuration plugin set the prohibited providers.
    /// </summary>
    /// <param name="luaList">The Lua table of provider names.</param>
    private static async Task ProcessAsync(string luaList)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    SETTINGS = {}
                                    SETTINGS["DataApp.ProhibitedProviders"] = {{luaList}}
                                    """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        var processed = ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ProhibitedProviders, PLUGIN_ID, settingsTable, false);
        Assert.That(processed, Is.True, "A list of names is a valid configuration of this setting.");
    }
}