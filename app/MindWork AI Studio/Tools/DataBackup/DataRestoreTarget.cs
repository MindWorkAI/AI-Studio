using SettingsVersion = AIStudio.Settings.Version;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Where a restore goes, and what it has to fit to.
/// </summary>
/// <param name="DataDirectory">The data folder.</param>
/// <param name="ConfigDirectory">The configuration folder.</param>
/// <param name="AppVersion">The version of this AI Studio. A backup of a later version is refused.</param>
/// <param name="SettingsVersion">The version of the settings this AI Studio writes. A backup with later settings is refused.</param>
/// <param name="OrganizationPluginFolders">The plugins an organization deployed among the user's own ones, cf. DataBackupContents.FindOrganizationPluginsAmongUserPlugins. They stay as they are.</param>
public sealed record DataRestoreTarget(string DataDirectory, string ConfigDirectory, string AppVersion, SettingsVersion SettingsVersion, IReadOnlyList<string> OrganizationPluginFolders);