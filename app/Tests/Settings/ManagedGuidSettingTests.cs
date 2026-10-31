using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a configuration plugin sets a managed setting which holds the ID of a provider, such as
/// the preselected provider or the transcription provider.
/// </summary>
/// <remarks>
/// An empty text selects nothing, which is also the default of these settings, so an organization locks a
/// selection to none with it. The settings are reached through Program.SERVICE_PROVIDER, as in the app,
/// because a managed setting writes its value and its lock there. The managed state of each setting is
/// static, which is why this fixture does not run alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ManagedGuidSettingTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("8b3f1c6e-2d94-4a07-b5e1-6f9c0a2d7e43");

    private const string USERS_PROVIDER_ID = "0f4d9a2c-7b16-4e85-a3c9-1d5e8b7f2a60";
    private const string ORGANIZATIONS_PROVIDER_ID = "4a7e2b9d-1c63-4f08-9e5a-b2d6c8f0e317";

    private const string PRESELECTED_PROVIDER = "DataApp.PreselectedProvider";
    private const string TRANSCRIPTION_PROVIDER = "DataApp.UseTranscriptionProvider";

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

    [TestCase(PRESELECTED_PROVIDER, "")]
    [TestCase(PRESELECTED_PROVIDER, "   ")]
    [TestCase(TRANSCRIPTION_PROVIDER, "")]
    public async Task AnEmptyIdLocksTheSelectionToNone(string settingName, string configuredText)
    {
        this.SetValue(settingName, USERS_PROVIDER_ID);
        var processed = await ProcessAsync(settingName, configuredText, allowUserOverride: false);

        var configMeta = GetMeta(settingName);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True, "An empty text is a valid choice: none.");
            Assert.That(this.GetValue(settingName), Is.Empty);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
        });
    }

    [Test]
    public async Task AnEmptyIdWithUserOverrideBecomesTheDefault()
    {
        this.SetValue(PRESELECTED_PROVIDER, USERS_PROVIDER_ID);
        var processed = await ProcessAsync(PRESELECTED_PROVIDER, string.Empty, allowUserOverride: true);

        var configMeta = GetMeta(PRESELECTED_PROVIDER);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(this.GetValue(PRESELECTED_PROVIDER), Is.Empty, "The default of the organization applies until the user chooses otherwise.");
            Assert.That(configMeta.IsLocked, Is.False);
            Assert.That(configMeta.ManagedMode, Is.EqualTo(ManagedConfigurationMode.EDITABLE_DEFAULT));
        });
    }

    [Test]
    public async Task AValidIdIsStoredInLowerCase()
    {
        var processed = await ProcessAsync(PRESELECTED_PROVIDER, ORGANIZATIONS_PROVIDER_ID.ToUpperInvariant(), allowUserOverride: false);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(this.GetValue(PRESELECTED_PROVIDER), Is.EqualTo(ORGANIZATIONS_PROVIDER_ID));
            Assert.That(GetMeta(PRESELECTED_PROVIDER).IsLocked, Is.True);
        });
    }

    [Test]
    public async Task AnInvalidIdIsIgnored()
    {
        this.SetValue(PRESELECTED_PROVIDER, USERS_PROVIDER_ID);
        var processed = await ProcessAsync(PRESELECTED_PROVIDER, "not-a-guid", allowUserOverride: false);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(this.GetValue(PRESELECTED_PROVIDER), Is.EqualTo(USERS_PROVIDER_ID), "An invalid ID leaves the choice of the user untouched.");
            Assert.That(GetMeta(PRESELECTED_PROVIDER).IsLocked, Is.False, "An invalid ID locks nothing.");
        });
    }

    [Test]
    public async Task TheLockEndsOnceThePluginNoLongerDeclaresTheSetting()
    {
        this.SetValue(PRESELECTED_PROVIDER, USERS_PROVIDER_ID);
        await ProcessAsync(PRESELECTED_PROVIDER, string.Empty, allowUserOverride: false);
        var processed = await ProcessAsync(PRESELECTED_PROVIDER, null, allowUserOverride: false);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(GetMeta(PRESELECTED_PROVIDER).IsLocked, Is.False);
            Assert.That(this.GetValue(PRESELECTED_PROVIDER), Is.EqualTo(USERS_PROVIDER_ID), "The user gets back the choice made before the plugin took the setting over.");
        });
    }

    private string GetValue(string settingName) => PropertyOf(settingName).Compile().Invoke(this.settingsManager.ConfigurationData.App);

    private void SetValue(string settingName, string value)
    {
        switch (settingName)
        {
            case PRESELECTED_PROVIDER:
                this.settingsManager.ConfigurationData.App.PreselectedProvider = value;
                break;

            case TRANSCRIPTION_PROVIDER:
                this.settingsManager.ConfigurationData.App.UseTranscriptionProvider = value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(settingName), settingName, "This test knows no such setting.");
        }
    }

    /// <summary>
    /// Gets the managed state of a setting.
    /// </summary>
    private static ConfigMeta<DataApp, string> GetMeta(string settingName)
    {
        if (!ManagedConfiguration.TryGet(x => x.App, PropertyOf(settingName), out var configMeta))
            throw new InvalidOperationException($"The setting '{settingName}' is not registered.");

        return configMeta;
    }

    /// <summary>
    /// Selects the property of a setting, the way PluginConfiguration hands it to ManagedConfiguration.
    /// </summary>
    private static Expression<Func<DataApp, string>> PropertyOf(string settingName) => settingName switch
    {
        PRESELECTED_PROVIDER => x => x.PreselectedProvider,
        TRANSCRIPTION_PROVIDER => x => x.UseTranscriptionProvider,

        _ => throw new ArgumentOutOfRangeException(nameof(settingName), settingName, "This test knows no such setting."),
    };

    /// <summary>
    /// Lets a configuration plugin set a setting which holds an ID, as PluginConfiguration does.
    /// </summary>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="configuredText">The text the plugin sets, or null for a plugin which does not declare the setting.</param>
    /// <param name="allowUserOverride">True when the plugin offers the value as an editable default instead of locking it.</param>
    /// <returns>Whether the plugin's value was taken.</returns>
    private static async Task<bool> ProcessAsync(string settingName, string? configuredText, bool allowUserOverride)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();

        var luaValue = configuredText is null ? "nil" : $"\"{configuredText}\"";
        await state.DoStringAsync($$"""
                                    SETTINGS = {}
                                    SETTINGS["{{settingName}}"] = {{luaValue}}
                                    SETTINGS["{{settingName}}.AllowUserOverride"] = {{(allowUserOverride ? "true" : "false")}}
                                    """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return ManagedConfiguration.TryProcessConfiguration(x => x.App, PropertyOf(settingName), Guid.Empty, PLUGIN_ID, settingsTable, false);
    }
}