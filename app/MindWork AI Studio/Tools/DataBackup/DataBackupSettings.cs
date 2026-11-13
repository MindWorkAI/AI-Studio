using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// The settings as a backup takes them along.
/// </summary>
public static class DataBackupSettings
{
    /// <summary>
    /// Copies the settings, leaving out every object which a configuration plugin manages.
    /// </summary>
    /// <remarks>
    /// A configuration plugin brings its providers, data sources, chat templates, profiles, and
    /// policies back on another computer, along with their API keys. Restored from a backup as well,
    /// they would compete with what the plugin brings, and without the plugin they would stay without
    /// their keys: secrets never go into a backup. That holds for the configuration plugins of an
    /// organization and for those the user installed, since a backup takes the latter along.<br/><br/>
    ///
    /// A setting which a configuration plugin locks stays in the copy, together with the plugin which
    /// locks it. After a restore, the clean-up following the start of the plugins treats it like the
    /// setting of a removed plugin, until the plugin locks it again. Without the plugin, the user gets
    /// back the value they had chosen before.<br/><br/>
    ///
    /// The copy is a round trip through the format in which the settings are stored, so it shares
    /// nothing with the settings AI Studio runs with. Like storing the settings, it reads them while
    /// the rest of the app may still change them.
    /// </remarks>
    /// <param name="settings">The settings AI Studio runs with. They stay as they are.</param>
    /// <returns>The copy for the backup.</returns>
    public static Data CopyWithoutPluginObjects(Data settings)
    {
        var copy = JsonSerializer.Deserialize<Data>(JsonSerializer.Serialize(settings, SettingsManager.JSON_OPTIONS), SettingsManager.JSON_OPTIONS)
                   ?? throw new InvalidOperationException("The settings could not be copied.");

        foreach (var descriptor in PluginConfigurationObjectDescriptors.ALL)
            descriptor.RemovePluginObjects(copy);

        return copy;
    }
}