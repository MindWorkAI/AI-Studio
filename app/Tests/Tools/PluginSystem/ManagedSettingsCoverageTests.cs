using System.Collections;
using System.Globalization;
using System.Text;

using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that a configuration plugin can manage every setting registered as a managed one.
/// </summary>
/// <remarks>
/// A data class registers a managed setting with ManagedConfiguration.Register, and
/// PluginConfiguration.TryProcessConfiguration reads it from a configuration plugin. Nothing ties the
/// two together: a setting registered but never read can be locked in the user interface and appears
/// in the documentation, yet no configuration plugin ever reaches it. The defaults of the Visual
/// Briefing assistant stayed out of reach of every organization that way.<br/><br/>
/// The test therefore configures every registered setting in one plugin and runs it for real. A dry
/// run would not do: it reads the values but applies none of them, so it cannot tell which settings
/// were read. The settings are reached through Program.SERVICE_PROVIDER, and the managed state of each
/// setting is static, which is why this fixture does not run alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ManagedSettingsCoverageTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("cd426cd3-bcff-4d6f-87bd-2c3c0756aa7d");

    /// <summary>
    /// The value configured for every setting holding a string.
    /// </summary>
    /// <remarks>
    /// Free text accepts any value, and the settings which hold the ID of a provider or a profile
    /// accept nothing but a GUID, so a GUID suits almost all of them. The others are listed in
    /// <see cref="SAMPLE_TEXTS_BY_SETTING"/>.
    /// </remarks>
    private const string SAMPLE_TEXT = "e2f4bebe-1a3a-4f98-bbb9-e40e0b9daa14";

    /// <summary>
    /// The values configured for the settings holding a string which accept no GUID, by the name of the setting.
    /// </summary>
    /// <remarks>
    /// A path relative to the plugin has to name a file the plugin ships, and the plugin of this test
    /// ships none. An absolute path is taken as the organization wrote it, so it suits these settings.
    /// Forward slashes keep the path free of escapes in Lua and work on every system.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> SAMPLE_TEXTS_BY_SETTING = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DataSlideBuilder.TemplatePath"] = Path.Combine(Path.GetTempPath(), "Corporate.potx").Replace('\\', '/'),
    };

    /// <summary>
    /// The registered settings this test does not expect a configuration plugin to manage, each with
    /// the reason.
    /// </summary>
    /// <remarks>
    /// A setting which must stay out of reach of configuration plugins belongs here as well. Neither
    /// kind of entry is a way to postpone reading a setting: a plugin which declares a setting that
    /// AI Studio silently ignores is worse than a setting no plugin can declare.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> EXEMPT_SETTINGS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DataAssistantPluginAudit.EnterpriseApprovedPlugins"] = "PluginConfiguration reads the approvals with a parser of its own, and only from a configuration plugin of an organization. Whether a plugin is one depends on the directory it lives in, which only the plugin factory sets up.",
    };

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Only builds its HTTP clients. Nothing connects, because the plugin of this test defines no provider:
        this.rustService = new RustService("1", "unused");

        // Creating the settings data registers every managed setting:
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(settingsManager).AddSingleton(this.rustService).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        //
        // The managed state of a setting outlives this fixture, so we release everything the plugin
        // took over. The clean-up AI Studio runs after a plugin was removed does exactly that, as
        // long as it still reaches the settings of this test:
        //
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public async Task EveryRegisteredSettingCanBeManagedByAConfigurationPlugin()
    {
        using var plugin = await RunPluginWithEveryRegisteredSettingAsync();
        var registeredSettings = ManagedConfiguration.RegisteredSettings.ToList();
        var unmanagedSettingNames = registeredSettings
            .Where(setting => !EXEMPT_SETTINGS.ContainsKey(setting.SettingName) && !IsManagedByThePlugin(setting))
            .Select(setting => setting.SettingName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(registeredSettings.Select(setting => setting.SettingName), Does.Contain("DataApp.UpdateInterval"), "No settings were registered, so this test would check nothing.");
            Assert.That(plugin.DeclaredSettingsCount, Is.EqualTo(registeredSettings.Count), "The plugin of this test does not declare every registered setting.");
            Assert.That(unmanagedSettingNames, Is.Empty, "These settings are registered with ManagedConfiguration.Register, but no configuration plugin can manage them. Read each one in PluginConfiguration.TryProcessConfiguration and document it in the example configuration plugin.lua, or add it to EXEMPT_SETTINGS with the reason it must stay out of reach of configuration plugins.");
        });
    }

    [Test]
    public async Task EveryExemptSettingIsRegisteredAndStaysUnmanaged()
    {
        using var plugin = await RunPluginWithEveryRegisteredSettingAsync();
        var registeredSettings = ManagedConfiguration.RegisteredSettings.ToDictionary(setting => setting.SettingName, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach (var exemptSettingName in EXEMPT_SETTINGS.Keys)
            {
                Assert.That(registeredSettings.TryGetValue(exemptSettingName, out var exemptSetting), Is.True, $"The exempt setting '{exemptSettingName}' is not registered anymore. Remove it from EXEMPT_SETTINGS.");
                if (exemptSetting is not null)
                    Assert.That(IsManagedByThePlugin(exemptSetting), Is.False, $"A configuration plugin can manage the exempt setting '{exemptSettingName}' by now. Remove it from EXEMPT_SETTINGS.");
            }
        });
    }

    /// <summary>
    /// Loads a configuration plugin which declares a valid value for every registered setting, and
    /// applies it the way AI Studio does when the plugin starts.
    /// </summary>
    /// <returns>The started configuration plugin.</returns>
    private static async Task<PluginConfiguration> RunPluginWithEveryRegisteredSettingAsync()
    {
        var code = new StringBuilder($$"""
            ID = "{{PLUGIN_ID}}"
            NAME = "Configuration for the tests"
            DESCRIPTION = "A configuration plugin which declares every managed setting."
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

        foreach (var setting in ManagedConfiguration.RegisteredSettings)
            code.AppendLine($"""CONFIG["SETTINGS"]["{setting.SettingName}"] = {ToLuaValue(setting)}""");

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
    /// Writes a value which the setting accepts, in the Lua notation of a configuration plugin.
    /// </summary>
    /// <remarks>
    /// Numbers take the default of the setting, because only the default is sure to pass the range
    /// a setting may check. Every collection accepts an empty table.
    /// </remarks>
    private static string ToLuaValue(ConfigMetaBase setting)
    {
        var valueType = GetValueType(setting);
        var defaultValue = setting.GetType().GetProperty(nameof(ConfigMeta<object, object>.Default))!.GetValue(setting);

        if (valueType == typeof(bool))
            return "true";

        if (valueType == typeof(string))
            return $"\"{SAMPLE_TEXTS_BY_SETTING.GetValueOrDefault(setting.SettingName, SAMPLE_TEXT)}\"";

        if (valueType.IsEnum)
            return $"\"{Enum.GetName(valueType, defaultValue!)}\"";

        if (valueType == typeof(int))
            return Convert.ToString(defaultValue, CultureInfo.InvariantCulture)!;

        if (typeof(IEnumerable).IsAssignableFrom(valueType))
            return "{}";

        throw new InvalidOperationException($"This test has no value for the setting '{setting.SettingName}' of type {valueType}. Extend ToLuaValue with a value which PluginConfiguration accepts for that type.");
    }

    /// <summary>
    /// Determines the type a setting holds.
    /// </summary>
    private static Type GetValueType(ConfigMetaBase setting)
    {
        for (var type = setting.GetType(); type is not null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ConfigMeta<,>))
                return type.GetGenericArguments()[1];

        throw new InvalidOperationException($"The setting '{setting.SettingName}' is no ConfigMeta, so this test cannot tell which type it holds.");
    }

    /// <summary>
    /// Checks whether the plugin of this test manages a setting: by locking it, or by contributing to it.
    /// </summary>
    private static bool IsManagedByThePlugin(ConfigMetaBase setting) => (setting.IsLocked && setting.LockedByConfigPluginId == PLUGIN_ID) || setting.ContributingConfigPluginIds.Contains(PLUGIN_ID);
}