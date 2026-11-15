using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that a configuration plugin sets the notes of an organization for the BeyondFacts Lab, and that the lab
/// takes no more of them than fits into every request.
/// </summary>
/// <remarks>
/// Each test runs a configuration plugin the way AI Studio starts one, so that it checks how PluginConfiguration
/// reads the notes as well. The settings are reached through Program.SERVICE_PROVIDER, and the managed state of each
/// setting is static, which is why this fixture does not run alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class BflOrganizationRulesSettingTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("8b2e6f14-3c7a-4d95-a0e8-5f1b9c2d7e63");

    private SettingsManager settingsManager = null!;
    private RustService rustService = null!;
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
        // The managed state of a setting outlives this fixture, so we release what the plugin took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public void WithoutAPluginTheLabHasNoNotes()
    {
        Assert.That(this.settingsManager.ConfigurationData.BeyondFactsLab.OrganizationRules, Is.Empty);
    }

    [Test]
    public async Task APluginSetsTheNotesWithLfLineBreaksAndWithoutSpacesAroundThem()
    {
        using var plugin = await RunPluginAsync("\"\\r\\n  - Contact the press office.\\r\\n- Consult your head of department.\\r\\n\\r\\n\"");
        ManagedConfiguration.TryGet(x => x.BeyondFactsLab, x => x.OrganizationRules, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.settingsManager.ConfigurationData.BeyondFactsLab.OrganizationRules, Is.EqualTo("- Contact the press office.\n- Consult your head of department."));
            Assert.That(configMeta.IsLocked, Is.True);
        }
    }

    [Test]
    public async Task ThePluginMayUseAllTheCharactersTheLabTakes()
    {
        var notes = new string('a', DataBeyondFactsLab.ORGANIZATION_RULES_MAX_CHARACTERS);
        using var plugin = await RunPluginAsync($"\"{notes}\"");

        Assert.That(this.settingsManager.ConfigurationData.BeyondFactsLab.OrganizationRules, Is.EqualTo(notes));
    }

    [Test]
    public async Task LongerNotesLeaveTheLabWithoutNotes()
    {
        var notes = new string('a', DataBeyondFactsLab.ORGANIZATION_RULES_MAX_CHARACTERS + 1);
        using var plugin = await RunPluginAsync($"\"{notes}\"");
        ManagedConfiguration.TryGet(x => x.BeyondFactsLab, x => x.OrganizationRules, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.settingsManager.ConfigurationData.BeyondFactsLab.OrganizationRules, Is.Empty);
            Assert.That(configMeta.IsLocked, Is.False);
        }
    }

    /// <summary>
    /// Loads a configuration plugin which sets the notes of the organization, and applies it the way AI Studio does
    /// when the plugin starts.
    /// </summary>
    /// <param name="luaValue">The notes, in the notation of Lua.</param>
    /// <returns>The started configuration plugin.</returns>
    private static async Task<PluginConfiguration> RunPluginAsync(string luaValue)
    {
        var code = $$"""
            ID = "{{PLUGIN_ID}}"
            NAME = "Configuration for the tests"
            DESCRIPTION = "A configuration plugin which sets the notes of an organization for the BeyondFacts Lab."
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
            CONFIG["SETTINGS"]["DataBeyondFactsLab.OrganizationRules"] = {{luaValue}}
            """;

        var plugin = await PluginFactory.Load(null, code);
        if (plugin is not PluginConfiguration { IsValid: true } configurationPlugin)
        {
            var issues = string.Join(" ", plugin.Issues);
            plugin.Dispose();
            throw new InvalidOperationException($"The code of this test did not produce a valid configuration plugin: {issues}");
        }

        await configurationPlugin.InitializeAsync(false);
        return configurationPlugin;
    }
}