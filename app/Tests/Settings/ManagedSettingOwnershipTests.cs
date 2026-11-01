using System.Linq.Expressions;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private const string PROHIBITED_PROVIDERS = "DataApp.ProhibitedProviders";
    private const string LOCKED_TOOL_SETTINGS = "DataTools.LockedToolSettings";
    private const string DEFAULT_TOOL_SETTINGS = "DataTools.DefaultToolSettings";
    private const string MINIMUM_PROVIDER_CONFIDENCE = "DataTools.MinimumProviderConfidenceByToolId";

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private Func<Guid, bool> previousIsOrganizationPlugin = null!;
    private ILoggerFactory previousLoggerFactory = null!;

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

        this.previousLoggerFactory = Program.LOGGER_FACTORY;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        // The managed state of a setting outlives this fixture, so we release what the plugins took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());
        ManagedConfiguration.IsOrganizationPlugin = this.previousIsOrganizationPlugin;
        Program.LOGGER_FACTORY = this.previousLoggerFactory;

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task ALocalPluginCannotTakeOverALockedSettingOfTheOrganization()
    {
        await ProcessProhibitedProvidersAsync(ORGANIZATION_PLUGIN_ID, """{ "DEEP_SEEK" }""");
        var processed = await ProcessProhibitedProvidersAsync(LOCAL_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False, "The setting of the organization is out of reach of a local plugin.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.DEEP_SEEK }), "The value of the organization has to stay.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the setting.");
        }
    }

    [Test]
    public async Task ALocalPluginCannotTakeOverAnEditableDefaultOfTheOrganization()
    {
        await ProcessUpdateIntervalAsync(ORGANIZATION_PLUGIN_ID, "WEEKLY", allowUserOverride: true);
        var processed = await ProcessUpdateIntervalAsync(LOCAL_PLUGIN_ID, "DAILY", allowUserOverride: false);

        ManagedConfiguration.TryGet(x => x.App, x => x.UpdateInterval, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False, "The editable default of the organization is out of reach of a local plugin.");
            Assert.That(this.settingsManager.ConfigurationData.App.UpdateInterval, Is.EqualTo(UpdateInterval.WEEKLY), "The value of the organization has to stay.");
            Assert.That(configMeta.IsLocked, Is.False, "A local plugin must not lock what the organization left to the user.");
            Assert.That(ManagedConfiguration.GetSettingOwner(configMeta), Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the setting.");
        }
    }

    [Test]
    public async Task AnotherOrganizationPluginMayTakeOverTheSetting()
    {
        await ProcessProhibitedProvidersAsync(ORGANIZATION_PLUGIN_ID, """{ "DEEP_SEEK" }""");
        var processed = await ProcessProhibitedProvidersAsync(SECOND_ORGANIZATION_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True, "Two configuration plugins of the same organization may override each other.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.MISTRAL }), "The plugin applied later wins.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(SECOND_ORGANIZATION_PLUGIN_ID));
        }
    }

    [Test]
    public async Task ALocalPluginManagesASettingNoOrganizationManages()
    {
        var processed = await ProcessProhibitedProvidersAsync(LOCAL_PLUGIN_ID, """{ "MISTRAL" }""");

        ManagedConfiguration.TryGet(x => x.App, x => x.ProhibitedProviders, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True, "Without an organization, a local plugin may manage the setting.");
            Assert.That(this.settingsManager.ConfigurationData.App.ProhibitedProviders, Is.EquivalentTo(new[] { LLMProviders.MISTRAL }));
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(LOCAL_PLUGIN_ID));
        }
    }

    [TestCase(LOCKED_TOOL_SETTINGS)]
    [TestCase(DEFAULT_TOOL_SETTINGS)]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE)]
    public async Task ALocalPluginCannotReplaceAToolTableOfTheOrganization(string settingName)
    {
        var entry = SampleEntryOf(settingName);
        await ProcessToolTableAsync(settingName, ORGANIZATION_PLUGIN_ID, ToLuaTable(entry.Key, entry.OrganizationValue), allowUserOverride: false);
        var processed = await ProcessToolTableAsync(settingName, LOCAL_PLUGIN_ID, ToLuaTable(entry.Key, entry.LocalValue), allowUserOverride: false);

        var configMeta = GetToolTableMeta(settingName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False, "The table of the organization is out of reach of a local plugin.");
            Assert.That(this.ToolTableOf(settingName), Is.EquivalentTo(new Dictionary<string, string> { [entry.Key] = entry.OrganizationValue }), "The values of the organization have to stay.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the table.");
        }
    }

    [TestCase(LOCKED_TOOL_SETTINGS)]
    [TestCase(DEFAULT_TOOL_SETTINGS)]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE)]
    public async Task ALocalPluginCannotTurnALockedToolTableIntoAnEditableDefault(string settingName)
    {
        var entry = SampleEntryOf(settingName);
        await ProcessToolTableAsync(settingName, ORGANIZATION_PLUGIN_ID, ToLuaTable(entry.Key, entry.OrganizationValue), allowUserOverride: false);
        var processed = await ProcessToolTableAsync(settingName, LOCAL_PLUGIN_ID, ToLuaTable(entry.Key, entry.LocalValue), allowUserOverride: true);

        var configMeta = GetToolTableMeta(settingName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False);
            Assert.That(configMeta.IsLocked, Is.True, "A local plugin must not hand the user a table the organization locked.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(ORGANIZATION_PLUGIN_ID));
            Assert.That(this.ToolTableOf(settingName), Is.EquivalentTo(new Dictionary<string, string> { [entry.Key] = entry.OrganizationValue }));
        }
    }

    [TestCase(LOCKED_TOOL_SETTINGS)]
    [TestCase(DEFAULT_TOOL_SETTINGS)]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE)]
    public async Task ALocalPluginCannotReplaceAnEditableDefaultToolTableOfTheOrganization(string settingName)
    {
        var entry = SampleEntryOf(settingName);
        await ProcessToolTableAsync(settingName, ORGANIZATION_PLUGIN_ID, ToLuaTable(entry.Key, entry.OrganizationValue), allowUserOverride: true);
        var processed = await ProcessToolTableAsync(settingName, LOCAL_PLUGIN_ID, ToLuaTable(entry.Key, entry.LocalValue), allowUserOverride: false);

        var configMeta = GetToolTableMeta(settingName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False);
            Assert.That(configMeta.IsLocked, Is.False, "A local plugin must not lock what the organization left to the user.");
            Assert.That(ManagedConfiguration.GetSettingOwner(configMeta), Is.EqualTo(ORGANIZATION_PLUGIN_ID), "The organization has to stay the owner of the table.");
            Assert.That(this.ToolTableOf(settingName), Is.EquivalentTo(new Dictionary<string, string> { [entry.Key] = entry.OrganizationValue }));
        }
    }

    [TestCase(LOCKED_TOOL_SETTINGS)]
    [TestCase(DEFAULT_TOOL_SETTINGS)]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE)]
    public async Task AnotherOrganizationPluginMayReplaceAToolTable(string settingName)
    {
        var entry = SampleEntryOf(settingName);
        await ProcessToolTableAsync(settingName, ORGANIZATION_PLUGIN_ID, ToLuaTable(entry.Key, entry.OrganizationValue), allowUserOverride: false);
        var processed = await ProcessToolTableAsync(settingName, SECOND_ORGANIZATION_PLUGIN_ID, ToLuaTable(entry.Key, entry.LocalValue), allowUserOverride: false);

        var configMeta = GetToolTableMeta(settingName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True, "Two configuration plugins of the same organization may override each other.");
            Assert.That(this.ToolTableOf(settingName), Is.EquivalentTo(new Dictionary<string, string> { [entry.Key] = entry.LocalValue }), "The plugin applied later wins.");
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(SECOND_ORGANIZATION_PLUGIN_ID));
        }
    }

    [TestCase(LOCKED_TOOL_SETTINGS)]
    [TestCase(DEFAULT_TOOL_SETTINGS)]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE)]
    public async Task ALocalPluginManagesAToolTableNoOrganizationManages(string settingName)
    {
        var entry = SampleEntryOf(settingName);
        var processed = await ProcessToolTableAsync(settingName, LOCAL_PLUGIN_ID, ToLuaTable(entry.Key, entry.LocalValue), allowUserOverride: false);

        var configMeta = GetToolTableMeta(settingName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True, "Without an organization, a local plugin may manage the table.");
            Assert.That(this.ToolTableOf(settingName), Is.EquivalentTo(new Dictionary<string, string> { [entry.Key] = entry.LocalValue }));
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(LOCAL_PLUGIN_ID));
        }
    }

    /// <summary>
    /// Checks that the user gets their own minimum confidence levels back once the organization's
    /// configuration plugin is gone.
    /// </summary>
    /// <remarks>
    /// Only this one of the tool tables holds values of the user: the tool settings of the user live
    /// elsewhere, and the two other tables belong to configuration plugins alone.
    /// </remarks>
    [TestCase(false)]
    [TestCase(true)]
    public async Task WithdrawingTheOrganizationHandsTheUsersConfidenceLevelsBack(bool allowUserOverride)
    {
        this.SetUsersConfidenceLevels(new() { ["read_web_page"] = "MEDIUM" });
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, ToLuaTable("read_web_page", "HIGH"), allowUserOverride);
        Assert.That(this.ToolTableOf(MINIMUM_PROVIDER_CONFIDENCE), Is.EquivalentTo(new Dictionary<string, string> { ["read_web_page"] = "HIGH" }), "While the organization manages the table, its level applies.");

        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Assert.That(this.ToolTableOf(MINIMUM_PROVIDER_CONFIDENCE), Is.EquivalentTo(new Dictionary<string, string> { ["read_web_page"] = "MEDIUM" }), "Without its plugin, the user has to get their own level back instead of the defaults.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DroppingTheTableFromTheConfigurationHandsTheUsersConfidenceLevelsBack(bool allowUserOverride)
    {
        this.SetUsersConfidenceLevels(new() { ["read_web_page"] = "MEDIUM" });
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, ToLuaTable("read_web_page", "HIGH"), allowUserOverride);

        // The plugin stays, but its next version no longer declares the table:
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, "nil", allowUserOverride);

        Assert.That(this.ToolTableOf(MINIMUM_PROVIDER_CONFIDENCE), Is.EquivalentTo(new Dictionary<string, string> { ["read_web_page"] = "MEDIUM" }), "Once no plugin manages the table, the user has to get their own level back.");
    }

    [Test]
    public async Task AnEditableDefaultTheUserChangedOutlivesTheOrganization()
    {
        this.SetUsersConfidenceLevels(new() { ["read_web_page"] = "MEDIUM" });
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, ToLuaTable("read_web_page", "HIGH"), allowUserOverride: true);
        this.SetUsersConfidenceLevels(new() { ["read_web_page"] = "LOW" });

        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Assert.That(this.ToolTableOf(MINIMUM_PROVIDER_CONFIDENCE), Is.EquivalentTo(new Dictionary<string, string> { ["read_web_page"] = "LOW" }), "The user decided against the default of the organization, and their decision outlives it.");
    }

    [Test]
    public async Task AnUpdatedEditableDefaultReachesUsersWhoKeptTheOldOne()
    {
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, ToLuaTable("read_web_page", "HIGH"), allowUserOverride: true);
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, ToLuaTable("read_web_page", "MODERATE"), allowUserOverride: true);

        Assert.That(this.ToolTableOf(MINIMUM_PROVIDER_CONFIDENCE), Is.EquivalentTo(new Dictionary<string, string> { ["read_web_page"] = "MODERATE" }), "Whoever kept the previous default gets the new one.");
    }

    /// <summary>
    /// Checks the form in which the last applied editable default of a tool table is stored.
    /// </summary>
    /// <remarks>
    /// The settings of installed versions hold this form. Another one would make every stored default
    /// look as if the user had changed it, so that the organization could never update it again.
    /// </remarks>
    [Test]
    public async Task TheStoredEditableDefaultOfAToolTableKeepsItsForm()
    {
        await ProcessToolTableAsync(MINIMUM_PROVIDER_CONFIDENCE, ORGANIZATION_PLUGIN_ID, """{ ["read_web_page"] = "HIGH", ["mailboxes"] = "MEDIUM" }""", allowUserOverride: true);

        var editableDefaultState = this.settingsManager.ConfigurationData.ManagedEditableDefaults[MINIMUM_PROVIDER_CONFIDENCE];
        Assert.That(editableDefaultState.LastAppliedValue, Is.EqualTo("mailboxes=MEDIUM\nread_web_page=HIGH"));
    }

    /// <summary>
    /// Checks that the log names a local configuration plugin only for a setting of the organization
    /// which the plugin actually declares.
    /// </summary>
    /// <remarks>
    /// Administrators read this warning as an attempt to take over what their organization configured.
    /// A local plugin which merely exists, without declaring the setting, attempts nothing, so warning
    /// about it would bury the real attempts under false ones: one for every setting of the organization,
    /// every time the plugins load. Both ways of handling a setting are covered, the one for sets and the
    /// one for single values and tables.
    /// </remarks>
    [TestCase(PROHIBITED_PROVIDERS, """{ "DEEP_SEEK" }""", """{ "MISTRAL" }""")]
    [TestCase(MINIMUM_PROVIDER_CONFIDENCE, """{ ["read_web_page"] = "HIGH" }""", """{ ["read_web_page"] = "NONE" }""")]
    public async Task ALocalPluginIsReportedOnlyForSettingsItDeclares(string settingName, string organizationValue, string localValue)
    {
        await ProcessSettingAsync(settingName, ORGANIZATION_PLUGIN_ID, organizationValue);

        var recorder = new WarningRecorder();
        Program.LOGGER_FACTORY = recorder;

        await ProcessSettingAsync(settingName, LOCAL_PLUGIN_ID, "nil");
        var warningsWithoutDeclaration = recorder.Warnings.ToList();

        await ProcessSettingAsync(settingName, LOCAL_PLUGIN_ID, localValue);
        var warningsWithDeclaration = recorder.Warnings.Skip(warningsWithoutDeclaration.Count).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warningsWithoutDeclaration, Is.Empty, "A local plugin which does not declare the setting attempts nothing.");
            Assert.That(warningsWithDeclaration, Has.Count.EqualTo(1), "A local plugin which declares the setting of the organization has to be reported.");
            Assert.That(warningsWithDeclaration, Has.All.Contains(settingName).And.All.Contains(LOCAL_PLUGIN_ID.ToString()), "The warning has to name the setting and the plugin.");
        }
    }

    /// <summary>
    /// Lets a configuration plugin set one of the settings which ALocalPluginIsReportedOnlyForSettingsItDeclares covers.
    /// </summary>
    private static Task<bool> ProcessSettingAsync(string settingName, Guid pluginId, string luaValue) => settingName switch
    {
        PROHIBITED_PROVIDERS => ProcessProhibitedProvidersAsync(pluginId, luaValue),

        _ => ProcessToolTableAsync(settingName, pluginId, luaValue, allowUserOverride: false),
    };

    /// <summary>
    /// Lets a configuration plugin set the prohibited providers, a setting which holds a set.
    /// </summary>
    private static async Task<bool> ProcessProhibitedProvidersAsync(Guid pluginId, string luaList)
    {
        var settings = await CreateSettingsAsync(PROHIBITED_PROVIDERS, luaList, allowUserOverride: false);
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
    /// Lets a configuration plugin set one of the tool tables, the settings which hold a table of texts.
    /// </summary>
    /// <param name="settingName">The name of the tool table.</param>
    /// <param name="pluginId">The configuration plugin.</param>
    /// <param name="luaTable">The table in Lua notation, or nil for a plugin which does not declare it.</param>
    /// <param name="allowUserOverride">True when the plugin offers the table as an editable default instead of locking it.</param>
    private static async Task<bool> ProcessToolTableAsync(string settingName, Guid pluginId, string luaTable, bool allowUserOverride)
    {
        var settings = await CreateSettingsAsync(settingName, luaTable, allowUserOverride);
        return ManagedConfiguration.TryProcessConfiguration(x => x.Tools, ToolTableProperty(settingName), pluginId, settings, false);
    }

    /// <summary>
    /// Gets the managed state of a tool table.
    /// </summary>
    private static ConfigMeta<DataTools, IDictionary<string, string>> GetToolTableMeta(string settingName)
    {
        if (!ManagedConfiguration.TryGet(x => x.Tools, ToolTableProperty(settingName), out var configMeta))
            throw new InvalidOperationException($"The tool table '{settingName}' is not registered.");

        return configMeta;
    }

    /// <summary>
    /// Selects the property of a tool table, the way PluginConfiguration hands it to ManagedConfiguration.
    /// </summary>
    private static Expression<Func<DataTools, IDictionary<string, string>>> ToolTableProperty(string settingName) => settingName switch
    {
        LOCKED_TOOL_SETTINGS => x => x.LockedToolSettings,
        DEFAULT_TOOL_SETTINGS => x => x.DefaultToolSettings,
        MINIMUM_PROVIDER_CONFIDENCE => x => x.MinimumProviderConfidenceByToolId,

        _ => throw new ArgumentOutOfRangeException(nameof(settingName), settingName, "This test knows no such tool table."),
    };

    /// <summary>
    /// Reads a tool table straight from the settings data.
    /// </summary>
    private Dictionary<string, string> ToolTableOf(string settingName) => settingName switch
    {
        LOCKED_TOOL_SETTINGS => this.settingsManager.ConfigurationData.Tools.LockedToolSettings,
        DEFAULT_TOOL_SETTINGS => this.settingsManager.ConfigurationData.Tools.DefaultToolSettings,
        MINIMUM_PROVIDER_CONFIDENCE => this.settingsManager.ConfigurationData.Tools.MinimumProviderConfidenceByToolId,

        _ => throw new ArgumentOutOfRangeException(nameof(settingName), settingName, "This test knows no such tool table."),
    };

    /// <summary>
    /// Sets the minimum confidence levels the user chose for the tools.
    /// </summary>
    /// <remarks>
    /// This assigns a new table instead of changing the present one: the settings data registered
    /// first in a test run shares its table with the default value of the setting, and changing it
    /// would change that default for every later test.
    /// </remarks>
    private void SetUsersConfidenceLevels(Dictionary<string, string> levels) => this.settingsManager.ConfigurationData.Tools.MinimumProviderConfidenceByToolId = new(levels, StringComparer.Ordinal);

    /// <summary>
    /// Chooses an entry which suits a tool table: a strict value of an organization, and a loose
    /// value of a local plugin.
    /// </summary>
    private static ToolTableEntry SampleEntryOf(string settingName) => settingName switch
    {
        MINIMUM_PROVIDER_CONFIDENCE => new("read_web_page", "HIGH", "NONE"),

        _ => new("read_web_page.freeAddressChoice", "OFF", "ON"),
    };

    private static string ToLuaTable(string key, string value) => $$"""{ ["{{key}}"] = "{{value}}" }""";

    /// <summary>
    /// An entry of a tool table, with the value an organization configures and the one a local plugin configures.
    /// </summary>
    private sealed record ToolTableEntry(string Key, string OrganizationValue, string LocalValue);

    /// <summary>
    /// Records the warnings ManagedConfiguration writes to the log.
    /// </summary>
    /// <remarks>
    /// Every other category gets a logger which writes nowhere. A type which keeps its logger in a
    /// static field, and happens to be initialized during the test, would otherwise keep this recorder
    /// for the rest of the test run.
    /// </remarks>
    private sealed class WarningRecorder : ILoggerFactory, ILogger
    {
        private readonly List<string> warnings = [];

        public IReadOnlyList<string> Warnings => this.warnings;

        public ILogger CreateLogger(string categoryName) => categoryName == nameof(ManagedConfiguration) ? this : NullLogger.Instance;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel is LogLevel.Warning)
                this.warnings.Add(formatter(state, exception));
        }
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