using AIStudio.Tools.DataBackup;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks the list of what AI Studio keeps in its folders, and which of it belongs into a backup.
/// </summary>
/// <remarks>
/// A backup takes along only what the list includes, and a restore replaces only that. An entry
/// listed twice, or a pattern among the included entries, would leave both open to guessing. The
/// plugins an organization deployed among the user's own ones do not show by their folder, so the
/// list has to find them by what they declare about themselves.
/// </remarks>
[TestFixture]
public sealed class DataBackupContentsTests
{
    private static readonly string DATA_DIRECTORY = Path.Combine(Path.GetTempPath(), "ai-studio-backup-contents");

    [Test]
    public void EveryEntryNamesItsPlaceOnce()
    {
        var places = DataBackupContents.ENTRIES.Select(entry => $"{entry.Location}:{entry.RelativePath}").ToList();

        Assert.That(places, Is.Unique);
    }

    [Test]
    public void AnIncludedEntryNamesOnePlaceExactly()
    {
        var paths = DataBackupContents.ENTRIES.Where(entry => entry.IsIncluded).Select(entry => entry.RelativePath).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths.Where(path => path.Contains('*')), Is.Empty, "A restore replaces exactly what the backup holds, so it has to know the names.");
            Assert.That(paths.Where(path => path.Contains('\\')), Is.Empty, "Paths use forward slashes on every OS.");
            Assert.That(paths.Where(path => path.StartsWith(DataBackupContents.SEPARATOR)), Is.Empty, "Paths are relative to their folder.");
        }
    }

    [Test]
    public void EveryKindAppearsInTheList()
    {
        var listedKinds = DataBackupContents.ENTRIES.Select(entry => entry.Kind).ToHashSet();

        Assert.That(Enum.GetValues<DataBackupEntryKind>(), Is.SubsetOf(listedKinds), "A kind without an entry means an entry was dropped from the list.");
    }

    [Test]
    public void APluginTheOrganizationDeployedAmongTheUsersPluginsIsFound()
    {
        var plugins = new[]
        {
            new AvailablePlugin(PluginFolder("company-assistant"), isManagedByConfigServer: true),
            new AvailablePlugin(PluginFolder("my-assistant"), isManagedByConfigServer: false),
            new AvailablePlugin(PluginFolder(".config", Guid.NewGuid().ToString(), "assistant"), isManagedByConfigServer: true),
            new AvailablePlugin(Path.Combine(Path.GetTempPath(), "ai-studio-plugins-elsewhere", "assistant"), isManagedByConfigServer: true),
        };

        var found = DataBackupContents.FindOrganizationPluginsAmongUserPlugins(plugins, DATA_DIRECTORY);

        Assert.That(found, Is.EqualTo(new[] { "plugins/company-assistant/" }), "The folder of the configuration server is left out as a whole already, and a plugin outside of the data folder is no part of a backup anyway.");
    }

    private static string PluginFolder(params string[] parts) => Path.Combine(DATA_DIRECTORY, "plugins", Path.Combine(parts));

    /// <summary>
    /// A plugin as the plugin factory lists it after loading it.
    /// </summary>
    private sealed class AvailablePlugin(string localPath, bool isManagedByConfigServer) : IAvailablePlugin
    {
        public string IconDataUrl => string.Empty;

        public PluginType Type => PluginType.ASSISTANT;

        public Guid Id { get; } = Guid.NewGuid();

        public string Name => "Assistant";

        public string Description => string.Empty;

        public PluginVersion Version => new(1, 0, 0);

        public DateTimeOffset? LastChanged => null;

        public string[] Authors => [];

        public string SupportContact => string.Empty;

        public string SourceURL => string.Empty;

        public PluginCategory[] Categories => [];

        public PluginTargetGroup[] TargetGroups => [];

        public bool IsMaintained => true;

        public string DeprecationMessage => string.Empty;

        public bool IsInternal => false;

        public string LocalPath => localPath;

        public bool IsManagedByConfigServer => isManagedByConfigServer;

        public Guid? ManagedConfigurationId => null;

        public int ConfigurationPriority => 0;
    }
}