using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks who owns a managed setting when configuration plugins of an organization and local ones
/// declare it.
/// </summary>
/// <remarks>
/// A local configuration plugin must never take over a setting an organization manages, while two
/// configuration plugins of the same organization may override each other. Whether a plugin speaks for
/// an organization depends on the directory it lives in, which only a running app has, so this fixture
/// tells ManagedConfiguration which of its plugin IDs belong to an organization. The settings are reached
/// through Program.SERVICE_PROVIDER, and the managed state of each setting is static, which is why this
/// fixture does not run alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ManagedSettingOwnershipTests
{
    private static readonly Guid ORGANIZATION_PLUGIN_ID = Guid.Parse("5d0e7c31-8a4f-4b62-9e17-2c6b8f4a1d93");
    private static readonly Guid SECOND_ORGANIZATION_PLUGIN_ID = Guid.Parse("a7c94e02-3b5d-4f81-8d26-7e1f0b9c4a58");
    private static readonly Guid LOCAL_PLUGIN_ID = Guid.Parse("e2b61f8d-4c07-4a9e-b3d5-90f8a6c2e714");

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private Func<Guid, bool> previousIsOrganizationPlugin = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Creating the settings data registers every managed setting:
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;

        this.previousIsOrganizationPlugin = ManagedConfiguration.IsOrganizationPlugin;
        ManagedConfiguration.IsOrganizationPlugin = pluginId => pluginId == ORGANIZATION_PLUGIN_ID || pluginId == SECOND_ORGANIZATION_PLUGIN_ID;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        // The managed state of a setting outlives this fixture, so we release what the plugins took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());
        ManagedConfiguration.IsOrganizationPlugin = this.previousIsOrganizationPlugin;

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task ALocalPluginCannotTakeOverALockedSettingOfTheOrganization()
    {
        await ProcessProhibitedProvidersAsync(ORGANIZATION_PLUGIN_ID, """{ "DEEP_SEEK" }""");
        var processed = await ProcessProhibitedProvidersAsync(LOCAL_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False, "The setting of the organization is out of reach of a local plugin.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.DEEP_SEEK }), "The value of the organization has to stay.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the setting.");
        });
    }

    [Test]
    public async Task ALocalPluginCannotTakeOverAnEditableDefaultOfTheOrganization()
    {
        await ProcessUpdateIntervalAsync(ORGANIZATION_PLUGIN_ID, "WEEKLY", allowUserOverride: true);
        var processed = await ProcessUpdateIntervalAsync(LOCAL_PLUGIN_ID, "DAILY", allowUserOverride: false);

        ManagedConfiguration.TryGet(x => x.App, x => x.UpdateInterval, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False, "The editable default of the organization is out of reach of a local plugin.");
            Assert.That(this.settingsManager.ConfigurationData.App.UpdateInterval, Is.EqualTo(UpdateInterval.WEEKLY), "The value of the organization has to stay.");
            Assert.That(configMeta.IsLocked, Is.False, "A local plugin must not lock what the organization left to the user.");
            Assert.That(ManagedConfiguration.GetSettingOwner(configMeta), Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the setting.");
        });
    }

    [Test]
    public async Task AnotherOrganizationPluginMayTakeOverTheSetting()
    {
        await ProcessProhibitedProvidersAsync(ORGANIZATION_PLUGIN_ID, """{ "DEEP_SEEK" }""");
        var processed = await ProcessProhibitedProvidersAsync(SECOND_ORGANIZATION_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True, "Two configuration plugins of the same organization may override each other.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.MISTRAL }), "The plugin applied later wins.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(SECOND_ORGANIZATION_PLUGIN_ID));
        });
    }

    [Test]
    public async Task ALocalPluginManagesASettingNoOrganizationManages()
    {
        var processed = await ProcessProhibitedProvidersAsync(LOCAL_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True, "Without an organization, a local plugin may manage the setting.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.MISTRAL }));
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(LOCAL_PLUGIN_ID));
        });
    }

    /// <summary>
    /// Lets a configuration plugin set the prohibited providers, a setting which holds a set.
    /// </summary>
    private static async Task<bool> ProcessProhibitedProvidersAsync(Guid pluginId, string luaList)
    {
        var settings = await CreateSettingsAsync("DataApp.ProhibitedProviders", luaList, allowUserOverride: false);
        return ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.ProhibitedProviders, pluginId, settings, false);
    }

    /// <summary>
    /// Lets a configuration plugin set the update interval, a setting which holds a single value.
    /// </summary>
    private static async Task<bool> ProcessUpdateIntervalAsync(Guid pluginId, string interval, bool allowUserOverride)
    {
        var settings = await CreateSettingsAsync("DataApp.UpdateInterval", $"\"{interval}\"", allowUserOverride);
        return ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInterval, pluginId, settings, false);
    }

    /// <summary>
    /// Builds the table of settings a configuration plugin declares.
    /// </summary>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="luaValue">The value of the setting in Lua notation.</param>
    /// <param name="allowUserOverride">True when the plugin offers the value as an editable default instead of locking it.</param>
    private static async Task<LuaTable> CreateSettingsAsync(string settingName, string luaValue, bool allowUserOverride)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    SETTINGS = {}
                                    SETTINGS["{{settingName}}"] = {{luaValue}}
                                    SETTINGS["{{settingName}}.AllowUserOverride"] = {{(allowUserOverride ? "true" : "false")}}
                                    """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return settingsTable;
    }
}