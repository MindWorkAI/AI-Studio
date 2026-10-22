using AIStudio.Provider;
using AIStudio.Settings;

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