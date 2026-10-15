namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Where a backup takes its files from.
/// </summary>
/// <param name="DataDirectory">The data folder.</param>
/// <param name="ConfigDirectory">The configuration folder.</param>
/// <param name="SettingsJson">The settings as the backup takes them along, cf. DataBackupSettings. They come from memory rather than from the disk, since the backup leaves out the objects of configuration plugins.</param>
/// <param name="IndexCopiesDirectory">The copies of the indexes, in the layout of the data folder, or an empty string when the backup leaves the indexes out. The indexes in the data folder change while AI Studio runs, so the backup never reads them there.</param>
/// <param name="OrganizationPluginFolders">The plugins an organization deployed among the user's own ones, cf. DataBackupContents.FindOrganizationPluginsAmongUserPlugins.</param>
public sealed record DataBackupSources(string DataDirectory, string ConfigDirectory, string SettingsJson, string IndexCopiesDirectory, IReadOnlyList<string> OrganizationPluginFolders);