using System.Text;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Lua;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SharedTools.Text;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that configuration plugins written for AI Studio v26.11.1 keep the seven settings of the AI
/// tasks which v26.11.2 renamed.
/// </summary>
/// <remarks>
/// Compatibility shim, remove after 2027-05-26, see documentation/compatibility-shims/2026-11-ai-task-setting-names.md.
/// The tests which run a plugin reach the settings through Program.SERVICE_PROVIDER, and the managed
/// state of each setting is static, which is why this fixture does not run alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class FormerSettingNamesTests
{
    private const string FORMER_NAME = "DataAssistantPluginAudit.PreselectedAgentProvider";
    private const string CURRENT_NAME = "DataAssistantPluginAudit.PreselectedProvider";
    private const string USER_OVERRIDE_SUFFIX = ".AllowUserOverride";
    private const string FORMER_PROVIDER_ID = "0b6f3c1e-58a2-4d7b-9e44-2f1c8a7d3b90";
    private const string CURRENT_PROVIDER_ID = "6d2e9a47-c3b1-4f08-a5d6-91e7b4c20f3a";

    private static readonly Guid PLUGIN_ID = Guid.Parse("3c8e51d7-9f2a-4b66-8d03-e7a1b5f49c28");

    private RustService rustService = null!;
    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Only builds its HTTP clients. Nothing connects, because the plugins of these tests define no provider:
        this.rustService = new RustService("1", "unused");

        // Creating the settings data registers every managed setting:
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).AddSingleton(this.rustService).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        // The managed state of a setting outlives this fixture, so we release everything the plugin took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public void EveryCurrentNameIsARegisteredSetting()
    {
        var registeredSettingNames = ManagedConfiguration.RegisteredSettings.Select(setting => setting.SettingName).ToHashSet(StringComparer.Ordinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(FormerSettingNames.CURRENT_NAMES, Has.Count.EqualTo(7), "AI Studio v26.11.1 knew seven of the renamed settings.");
            foreach (var (formerName, currentName) in FormerSettingNames.CURRENT_NAMES)
            {
                Assert.That(registeredSettingNames, Does.Contain(currentName), $"The former name '{formerName}' leads to '{currentName}', which is no registered setting.");
                Assert.That(registeredSettingNames, Does.Not.Contain(formerName), $"The former name '{formerName}' is a registered setting again.");
            }
        }
    }

    [Test]
    public void AFormerNameTakesTheCurrentName()
    {
        var settingsTable = new LuaTable
        {
            [FORMER_NAME] = FORMER_PROVIDER_ID,
            [$"{FORMER_NAME}{USER_OVERRIDE_SUFFIX}"] = true,
            ["DataApp.UpdateInterval"] = "ONCE_STARTUP",
        };

        var resolvedTable = FormerSettingNames.Resolve(settingsTable, "Configuration for the tests", false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadNames(resolvedTable), Is.EquivalentTo(new[] { CURRENT_NAME, $"{CURRENT_NAME}{USER_OVERRIDE_SUFFIX}", "DataApp.UpdateInterval" }));
            Assert.That(resolvedTable[CURRENT_NAME].Read<string>(), Is.EqualTo(FORMER_PROVIDER_ID));
            Assert.That(resolvedTable[$"{CURRENT_NAME}{USER_OVERRIDE_SUFFIX}"].Read<bool>(), Is.True);
            Assert.That(resolvedTable["DataApp.UpdateInterval"].Read<string>(), Is.EqualTo("ONCE_STARTUP"));
        }
    }

    [Test]
    public void TheCurrentNameWinsOverTheFormerName()
    {
        var settingsTable = new LuaTable
        {
            [FORMER_NAME] = FORMER_PROVIDER_ID,
            [CURRENT_NAME] = CURRENT_PROVIDER_ID,
            [$"{FORMER_NAME}{USER_OVERRIDE_SUFFIX}"] = true,
            [$"{CURRENT_NAME}{USER_OVERRIDE_SUFFIX}"] = false,
        };

        var resolvedTable = FormerSettingNames.Resolve(settingsTable, "Configuration for the tests", false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ReadNames(resolvedTable), Is.EquivalentTo(new[] { CURRENT_NAME, $"{CURRENT_NAME}{USER_OVERRIDE_SUFFIX}" }));
            Assert.That(resolvedTable[CURRENT_NAME].Read<string>(), Is.EqualTo(CURRENT_PROVIDER_ID));
            Assert.That(resolvedTable[$"{CURRENT_NAME}{USER_OVERRIDE_SUFFIX}"].Read<bool>(), Is.False);
        }
    }

    [Test]
    public async Task AFormerNameLocksTheSetting()
    {
        using var plugin = await RunPluginAsync($"""CONFIG["SETTINGS"]["{FORMER_NAME}"] = "{FORMER_PROVIDER_ID}" """);
        var isRegistered = ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.PreselectedProvider, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRegistered, Is.True);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(this.settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedProvider, Is.EqualTo(FORMER_PROVIDER_ID));
            Assert.That(plugin.DeclaredSettingsCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AFormerNameWithUserOverrideSetsAnOrganizationDefault()
    {
        using var plugin = await RunPluginAsync(
            $"""CONFIG["SETTINGS"]["{FORMER_NAME}"] = "{FORMER_PROVIDER_ID}" """,
            $"""CONFIG["SETTINGS"]["{FORMER_NAME}{USER_OVERRIDE_SUFFIX}"] = true""");

        var isRegistered = ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.PreselectedProvider, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRegistered, Is.True);
            Assert.That(configMeta.IsLocked, Is.False);
            Assert.That(configMeta.ManagedMode, Is.EqualTo(ManagedConfigurationMode.EDITABLE_DEFAULT));
            Assert.That(configMeta.EditableDefaultByConfigPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(this.settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedProvider, Is.EqualTo(FORMER_PROVIDER_ID));
            Assert.That(plugin.DeclaredSettingsCount, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// An organization sets both names while its devices run v26.11.1 and v26.11.2 alike.
    /// </summary>
    [Test]
    public async Task APluginWhichSetsBothNamesCountsTheSettingOnce()
    {
        using var plugin = await RunPluginAsync(
            $"""CONFIG["SETTINGS"]["{FORMER_NAME}"] = "{FORMER_PROVIDER_ID}" """,
            $"""CONFIG["SETTINGS"]["{CURRENT_NAME}"] = "{CURRENT_PROVIDER_ID}" """);

        var isRegistered = ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.PreselectedProvider, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRegistered, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(this.settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedProvider, Is.EqualTo(CURRENT_PROVIDER_ID));
            Assert.That(plugin.DeclaredSettingsCount, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// Loads a configuration plugin with the given settings and applies it the way AI Studio does when the plugin starts.
    /// </summary>
    /// <param name="settingLines">The Lua lines which set the settings.</param>
    /// <returns>The started configuration plugin.</returns>
    private static async Task<PluginConfiguration> RunPluginAsync(params string[] settingLines)
    {
        var code = new StringBuilder($$"""
            ID = "{{PLUGIN_ID}}"
            NAME = "Configuration for the tests"
            DESCRIPTION = "A configuration plugin which uses the former names of settings."
            VERSION = "1.0.0"
            TYPE = "CONFIGURATION"
            AUTHORS = {"MindWork AI"}
            SUPPORT_CONTACT = "MindWork AI"
            SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
            CATEGORIES = {"CORE"}
            TARGET_GROUPS = {"EVERYONE"}
            IS_MAINTAINED = true
            DEPRECATION_MESSAGE = ""
            CONFIG = {}
            CONFIG["SETTINGS"] = {}

            """);

        foreach (var settingLine in settingLines)
            code.AppendLineLF(settingLine);

        var plugin = await PluginFactory.Load(null, code.ToString());
        if (plugin is not PluginConfiguration { IsValid: true } configurationPlugin)
        {
            var issues = string.Join(" ", plugin.Issues);
            plugin.Dispose();
            throw new InvalidOperationException($"The code of this test did not produce a valid configuration plugin: {issues}");
        }

        await configurationPlugin.InitializeAsync(false);
        return configurationPlugin;
    }

    /// <summary>
    /// Reads the names of all settings in a SETTINGS table.
    /// </summary>
    private static List<string> ReadNames(LuaTable settingsTable)
    {
        var names = new List<string>();
        var previousKey = LuaValue.Nil;
        while (settingsTable.TryGetNext(previousKey, out var pair))
        {
            previousKey = pair.Key;
            names.Add(pair.Key.Read<string>());
        }

        return names;
    }
}