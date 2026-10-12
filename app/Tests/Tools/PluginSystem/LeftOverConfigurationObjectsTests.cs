using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks which objects of a configuration plugin the clean-up removes from the settings.
/// </summary>
/// <remarks>
/// After every start of the plugins, the clean-up compares the objects in the settings with the ones
/// the configuration plugins defined, and removes what no plugin defines anymore. A plugin which was
/// loaded but did not start has defined nothing at all. Its objects must not count as dropped: on a
/// slow machine, where the time ran out before the plugin got its turn, that deleted the providers,
/// chat templates, and profiles of an organization on every start, their API keys included.<br/><br/>
/// The settings are reached through Program.SERVICE_PROVIDER, which is why this fixture does not run
/// alongside others.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class LeftOverConfigurationObjectsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("5c1f0e7a-2b9d-4c3e-8f6a-1d2e3f4a5b6c");

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;
    private SettingsManager settingsManager = null!;

    [SetUp]
    public void CreateSettingsWithATemplateOfThePlugin()
    {
        // Only builds its HTTP clients. Nothing connects, because a chat template has no secret:
        this.rustService = new RustService("1", "unused");
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);
        this.settingsManager.ConfigurationData.ChatTemplates.Add(new ChatTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Template of the organization",
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = PLUGIN_ID,
        });

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
    public async Task TheObjectsOfAPluginWhichDidNotStartStay()
    {
        var wasChanged = await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, [new AvailablePlugin()], new HashSet<Guid>(), new HashSet<Guid> { PLUGIN_ID }, []);

        Assert.Multiple(() =>
        {
            Assert.That(wasChanged, Is.False);
            Assert.That(this.settingsManager.ConfigurationData.ChatTemplates, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TheObjectsOfAPluginWhichStartedWithoutThemAreRemoved()
    {
        var wasChanged = await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, [new AvailablePlugin()], new HashSet<Guid>(), new HashSet<Guid>(), []);

        Assert.Multiple(() =>
        {
            Assert.That(wasChanged, Is.True, "A plugin which started and no longer defines the template has dropped it, or the test above checks nothing.");
            Assert.That(this.settingsManager.ConfigurationData.ChatTemplates, Is.Empty);
        });
    }

    /// <summary>
    /// The configuration plugin as the plugin factory lists it after loading it.
    /// </summary>
    private sealed class AvailablePlugin : IAvailablePlugin
    {
        public string IconDataUrl => string.Empty;

        public PluginType Type => PluginType.CONFIGURATION;

        public Guid Id => PLUGIN_ID;

        public string Name => "Configuration of the organization";

        public string Description => string.Empty;

        public PluginVersion Version => new(1, 0, 0);

        public string[] Authors => [];

        public string SupportContact => string.Empty;

        public string SourceURL => string.Empty;

        public PluginCategory[] Categories => [];

        public PluginTargetGroup[] TargetGroups => [];

        public bool IsMaintained => true;

        public string DeprecationMessage => string.Empty;

        public bool IsInternal => false;

        public string LocalPath => string.Empty;

        public bool IsManagedByConfigServer => false;

        public Guid? ManagedConfigurationId => null;

        public int ConfigurationPriority => 0;
    }
}